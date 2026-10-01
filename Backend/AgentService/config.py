from pydantic_settings import BaseSettings
from functools import lru_cache


class Settings(BaseSettings):
    # Azure OpenAI
    azure_openai_endpoint: str
    azure_openai_api_key: str
    azure_openai_chat_deployment: str = "gpt-4.1-mini"
    azure_openai_embedding_deployment: str = "text-embedding-3-small"
    azure_openai_api_version: str = "2024-08-01-preview"

    # PostgreSQL
    database_url: str  

    # RabbitMQ
    rabbitmq_url: str = "amqp://guest:guest@localhost:5672/"

    # C# API
    csharp_api_url: str = "http://portfolioapi.railway.internal:8080"
    csharp_api_internal_secret: str  # shared secret for service-to-service calls

    # Axiom log shipping. Leave unset locally for console-only logs
    axiom_token: str | None = None
    axiom_dataset: str | None = None

    # Agent service settings
    max_rag_results: int = 8
    max_response_tokens: int = 600

    class Config:
        env_file = ".env"
        env_file_encoding = "utf-8"


@lru_cache()
def get_settings() -> Settings:
    return Settings()
