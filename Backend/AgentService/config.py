from pydantic_settings import BaseSettings
from functools import lru_cache


class Settings(BaseSettings):
    # Azure OpenAI
    # No API key: auth is Entra ID (see llm.py)
    azure_openai_endpoint: str
    # Deployments are named by role in infra/main.bicepparam. The *_MODEL settings say
    # which model is behind each one, for picking call parameters and labeling results.
    # On Railway all of these are set by infra/scripts/sync-railway-vars.sh from the
    # Bicep deployment outputs, so don't edit them in the dashboard.
    azure_openai_chat_deployment: str = "chat"
    azure_openai_chat_model: str = "gpt-4.1-mini"
    azure_openai_embedding_deployment: str = "embedding"
    azure_openai_embedding_model: str = "text-embedding-3-small"
    azure_openai_api_version: str = "2024-08-01-preview"

    # Adversarial test judge and resume analysis. Ideally a different, stronger model
    # than the one it grades. Unset falls back to chat. Newer reasoning models
    # (gpt-5, o-series) may need a newer API version.
    azure_openai_judge_deployment: str | None = None
    azure_openai_judge_model: str | None = None
    azure_openai_judge_api_version: str | None = None

    # PostgreSQL
    database_url: str  

    # C# API
    csharp_api_url: str = "http://portfolioapi.railway.internal:8080"
    csharp_api_internal_secret: str  # shared secret for service-to-service calls

    # Traces and logs to App Insights (telemetry.py). Unset means no telemetry. Set on
    # Railway by infra/scripts/sync-railway-vars.sh. Not a secret: ingestion is Entra-only.
    applicationinsights_connection_string: str | None = None

    # Gmail API for recruiter triage, from scripts/gmail_auth.py. Unset means the
    # triage loop stays off.
    gmail_client_id: str | None = None
    gmail_client_secret: str | None = None
    gmail_refresh_token: str | None = None

    # Agent service settings
    max_rag_results: int = 8
    max_response_tokens: int = 600

    @property
    def judge_deployment(self) -> str:
        return self.azure_openai_judge_deployment or self.azure_openai_chat_deployment

    @property
    def judge_model(self) -> str:
        """The model behind judge_deployment. Falls back to the deployment name, which
        was the model name back when deployments were named after their model."""
        if self.azure_openai_judge_deployment:
            return self.azure_openai_judge_model or self.azure_openai_judge_deployment
        return self.azure_openai_chat_model

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        # pydantic-settings rejects unknown keys in .env by default, so removing a
        # setting (like RABBITMQ_URL) would crash startup for anyone who still has it
        extra = "ignore"


@lru_cache()
def get_settings() -> Settings:
    return Settings()
