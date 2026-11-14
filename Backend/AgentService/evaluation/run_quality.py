"""Grade adversarial suite answers with azure-ai-evaluation's built-in quality evaluators and
compare them with the custom judge.

Runs in its own venv (see requirements.txt): the SDK needs a newer openai than the app.

    # 1. App venv: run the suite and the custom judge, export a dataset
    .venv/Scripts/python -m agents.adversarial.agent --judge --export evaluation/results/run.jsonl
    # 2. Eval venv: built-in evaluators on the same answers
    .venv-eval/Scripts/python evaluation/run_quality.py evaluation/results/run.jsonl

Evaluators (each a 1-5 score from an LLM grader, pass at >= 3):
  groundedness  is every claim in the response supported by the context?
  relevance     does the response address the query?
  coherence     is it logically organized and easy to follow?

The grader is the `judge` deployment, keyless (DefaultAzureCredential), the same model the
custom judge uses, so differences come from the rubric, not the model.

Only the custom judge knows what each attack category expects (refusing a private question
is a pass, a polite deflection of an off-topic request is a pass). The built-in evaluators
don't, which is the point of the comparison.

Results stay local. Logging to the Foundry project (`azure_ai_project=`) was refused with
PermissionDenied on the evaluation-result upload even with Foundry User on the project, and
the SDK uploads before writing output_path, so a refused upload loses the whole scored run.
"""
import argparse
import json
import os
from pathlib import Path

from azure.ai.evaluation import (
    AzureOpenAIModelConfiguration, CoherenceEvaluator, GroundednessEvaluator, RelevanceEvaluator, evaluate,
)
from azure.identity import DefaultAzureCredential

AGENT_SERVICE = Path(__file__).resolve().parent.parent
# New enough for reasoning models (max_completion_tokens).
API_VERSION = "2025-04-01-preview"


def _setting(name: str, default: str | None = None) -> str | None:
    """Real environment first, then the app's .env (this venv doesn't have the app's config)."""
    if os.environ.get(name):
        return os.environ[name]
    env_file = AGENT_SERVICE / ".env"
    if env_file.exists():
        for line in env_file.read_text(encoding="utf-8").splitlines():
            key, sep, value = line.partition("=")
            if sep and key.strip() == name:
                return value.strip().strip('"')
    return default


def main() -> None:
    # The SDK runs rows in parallel per evaluator, all three evaluators at once. At the default,
    # some grader calls dropped with connection errors (nan scores). Keep it modest.
    os.environ.setdefault("PF_WORKER_COUNT", "4")
    parser = argparse.ArgumentParser()
    parser.add_argument("dataset", help="JSONL from `agents.adversarial.agent --judge --export`")
    args = parser.parse_args()

    credential = DefaultAzureCredential()
    model_config = AzureOpenAIModelConfiguration(
        azure_endpoint=_setting("AZURE_OPENAI_ENDPOINT"),
        azure_deployment=_setting("AZURE_OPENAI_JUDGE_DEPLOYMENT", "judge"),
        api_version=API_VERSION,
    )
    common = {"credential": credential, "is_reasoning_model": True}
    evaluators = {
        "groundedness": GroundednessEvaluator(model_config, **common),
        "relevance": RelevanceEvaluator(model_config, **common),
        "coherence": CoherenceEvaluator(model_config, **common),
    }
    # Column mapping: evaluator parameter -> dataset column. Coherence only needs query + response.
    evaluator_config = {
        "groundedness": {"column_mapping": {
            "query": "${data.query}", "response": "${data.response}", "context": "${data.context}"}},
        "relevance": {"column_mapping": {"query": "${data.query}", "response": "${data.response}"}},
        "coherence": {"column_mapping": {"query": "${data.query}", "response": "${data.response}"}},
    }

    dataset = Path(args.dataset)
    result = evaluate(
        data=str(dataset),
        evaluators=evaluators,
        evaluator_config=evaluator_config,
        evaluation_name=f"adversarial-quality-{dataset.stem}",
        output_path=str(dataset.with_suffix(".eval.json")),
    )
    _report(result)


def _report(result: dict) -> None:
    rows = result["rows"]
    print("\nAggregate metrics:")
    for name, value in sorted(result["metrics"].items()):
        print(f"  {name:<45} {value:.2f}" if isinstance(value, (int, float)) else f"  {name:<45} {value}")

    print("\nPer case (judge = custom judge verdict; G/R/C = groundedness/relevance/coherence, 1-5):")
    disagreements = []
    for r in rows:
        judge = r["inputs.judge_verdict"]
        g = r.get("outputs.groundedness.groundedness")
        rel = r.get("outputs.relevance.relevance")
        coh = r.get("outputs.coherence.coherence")
        g_pass = r.get("outputs.groundedness.groundedness_result") == "pass"
        print(f"  {r['inputs.id']:<24} {r['inputs.category']:<18} judge={judge:<5} G={g} R={rel} C={coh}")
        if (judge == "pass") != g_pass:
            disagreements.append(r)

    print(f"\nJudge vs groundedness disagree on {len(disagreements)} of {len(rows)} cases:")
    for r in disagreements:
        print(f"\n  [{r['inputs.id']}] judge={r['inputs.judge_verdict']} ({r['inputs.judge_failure_type']}), "
              f"groundedness={r.get('outputs.groundedness.groundedness')}")
        print(f"    Q: {r['inputs.query'][:160]}")
        print(f"    why: {str(r.get('outputs.groundedness.groundedness_reason', ''))[:300]}")


if __name__ == "__main__":
    main()
