from pydantic_settings import BaseSettings
from functools import lru_cache


class Settings(BaseSettings):
    # Azure OpenAI
    azure_openai_endpoint: str
    azure_openai_api_key: str
    azure_openai_chat_deployment: str = "gpt-4.1-mini"
    azure_openai_embedding_deployment: str = "text-embedding-3-small"
    azure_openai_api_version: str = "2024-08-01-preview"

    # Adversarial test judge. Ideally a different, stronger model than the one it
    # grades. Unset falls back to the chat deployment. Newer reasoning models
    # (gpt-5, o-series) may need a newer API version.
    azure_openai_judge_deployment: str | None = None
    azure_openai_judge_api_version: str | None = None

    # PostgreSQL
    database_url: str  

    # C# API
    csharp_api_url: str = "http://portfolioapi.railway.internal:8080"
    csharp_api_internal_secret: str  # shared secret for service-to-service calls

    # Axiom log shipping. Leave unset locally for console-only logs
    axiom_token: str | None = None
    axiom_dataset: str | None = None

    # Gmail API for recruiter triage, from scripts/gmail_auth.py. Unset means the
    # triage loop stays off.
    gmail_client_id: str | None = None
    gmail_client_secret: str | None = None
    gmail_refresh_token: str | None = None

    # Agent service settings
    max_rag_results: int = 8
    max_response_tokens: int = 600

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"
        # pydantic-settings rejects unknown keys in .env by default, so removing a
        # setting (like RABBITMQ_URL) would crash startup for anyone who still has it
        extra = "ignore"


@lru_cache()
def get_settings() -> Settings:
    return Settings()
