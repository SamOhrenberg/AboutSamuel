"""The only place Azure OpenAI clients are built.

Auth is Entra ID: a bearer token from DefaultAzureCredential, which needs the caller to hold
Cognitive Services OpenAI User on the Foundry account (infra/bootstrap). Which credential
answers is pinned by the AZURE_TOKEN_CREDENTIALS env var:
  - Railway: EnvironmentCredential, i.e. the sp-aboutsamuel-railway service principal from
    AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET.
  - Local: unset, so the chain ends at `az login`.
These are real environment variables, not .env entries: pydantic-settings reads .env into
Settings but never exports it to os.environ, which is all azure-identity looks at.

There's no key fallback: the Foundry account has disableLocalAuth on, so keys get a 401.
"""
from functools import lru_cache
from typing import Literal

import structlog
from azure.identity import DefaultAzureCredential, get_bearer_token_provider
from langchain_openai import AzureChatOpenAI, AzureOpenAIEmbeddings

from config import get_settings

logger = structlog.get_logger()

# The token audience for every Azure OpenAI / Foundry inference call.
COGNITIVE_SERVICES_SCOPE = "https://cognitiveservices.azure.com/.default"

# gpt-5 and o-series only accept their default temperature of 1, and want
# max_completion_tokens, which langchain-openai 0.2.14 can't send, so max_tokens is dropped.
REASONING_MODEL_PREFIXES = ("gpt-5", "o1", "o3", "o4")

Role = Literal["chat", "judge"]


@lru_cache()
def _auth() -> dict:
    """Client kwargs for auth. One credential per process: the provider caches the token
    until near expiry, so most calls never reach Entra. It's the sync provider on purpose;
    langchain hands it to the async client too, and a refresh about once an hour briefly
    blocking the loop beats adding aiohttp for azure.identity.aio."""
    logger.info("azure_openai_auth", mode="entra_id")
    provider = get_bearer_token_provider(DefaultAzureCredential(), COGNITIVE_SERVICES_SCOPE)
    return {"azure_ad_token_provider": provider}


def is_reasoning_model(model: str) -> bool:
    return model.lower().startswith(REASONING_MODEL_PREFIXES)


def model_name(role: Role) -> str:
    """The model behind a role-named deployment, for labeling results."""
    settings = get_settings()
    return settings.judge_model if role == "judge" else settings.azure_openai_chat_model


def chat_model(
    role: Role = "chat",
    *,
    max_tokens: int | None = None,
    temperature: float = 0,
    streaming: bool = False,
) -> AzureChatOpenAI:
    """A chat model on the deployment for `role`. Pass what a normal model should get;
    reasoning models get temperature 1 and no max_tokens regardless."""
    settings = get_settings()
    if role == "judge":
        deployment = settings.judge_deployment
        api_version = settings.azure_openai_judge_api_version or settings.azure_openai_api_version
    else:
        deployment = settings.azure_openai_chat_deployment
        api_version = settings.azure_openai_api_version

    if is_reasoning_model(model_name(role)):
        temperature, max_tokens = 1, None

    # A stream only reports token usage (in a final chunk) when asked. Without it, traces
    # of streamed calls have no token counts. AzureChatOpenAI 0.2.14 has no stream_usage
    # field, and the API rejects stream_options on non-streamed calls.
    model_kwargs = {"stream_options": {"include_usage": True}} if streaming else {}

    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        azure_deployment=deployment,
        api_version=api_version,
        max_tokens=max_tokens,
        temperature=temperature,
        streaming=streaming,
        model_kwargs=model_kwargs,
        # Azure routes by deployment and ignores this. It labels the trace span
        # (gen_ai.request.model), which would otherwise be blank.
        model=model_name(role),
        **_auth(),
    )


def embeddings() -> AzureOpenAIEmbeddings:
    settings = get_settings()
    return AzureOpenAIEmbeddings(
        azure_endpoint=settings.azure_openai_endpoint,
        azure_deployment=settings.azure_openai_embedding_deployment,
        # Labels the trace span. Without it langchain sends its text-embedding-ada-002 default.
        model=settings.azure_openai_embedding_model,
        api_version=settings.azure_openai_api_version,
        **_auth(),
    )
