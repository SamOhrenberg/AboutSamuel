from typing import AsyncIterator
from langchain_openai import AzureChatOpenAI
from langchain_core.messages import HumanMessage, AIMessage, SystemMessage
from langgraph.graph import StateGraph, MessagesState, END
from langgraph.prebuilt import ToolNode, tools_condition
from agents.samuellm.prompts import SYSTEM_PROMPT
from tools.retrieval import get_portfolio_overview_cached, search_experience
from tools.contact import contact_samuel, get_resume, redirect_to_page, ask_clarification
from config import get_settings
import structlog

logger = structlog.get_logger()

TOOLS = [search_experience, contact_samuel, get_resume, redirect_to_page, ask_clarification]


def _build_llm() -> AzureChatOpenAI:
    settings = get_settings()
    return AzureChatOpenAI(
        azure_endpoint=settings.azure_openai_endpoint,
        api_key=settings.azure_openai_api_key,
        azure_deployment=settings.azure_openai_chat_deployment,
        api_version=settings.azure_openai_api_version,
        max_tokens=settings.max_response_tokens,
        streaming=True,
        temperature=0.3,
    )


def build_graph(tools: list = TOOLS):
    """The live site uses the real tools. The adversarial tests pass a copy where
    contact_samuel is a fake, so a test can't send Samuel a real email."""
    llm = _build_llm()
    llm_with_tools = llm.bind_tools(tools)

    async def agent_node(state: MessagesState):
        # Prepend the system prompt on every call, with the complete timeline and project
        # list. Search only returns a few passages, so without this SamuelLM can't tell
        # "Google isn't an employer" from "Google just didn't come up in the search".
        overview = await get_portfolio_overview_cached()
        system = SYSTEM_PROMPT + (f"\nPORTFOLIO FACTS (complete):\n{overview}\n" if overview else "")
        response = await llm_with_tools.ainvoke([SystemMessage(content=system)] + state["messages"])
        return {"messages": [response]}

    tool_node = ToolNode(tools)

    workflow = StateGraph(MessagesState)
    workflow.add_node("agent", agent_node)
    workflow.add_node("tools", tool_node)
    workflow.set_entry_point("agent")
    workflow.add_conditional_edges("agent", tools_condition)
    workflow.add_edge("tools", "agent")

    return workflow.compile()


# Single graph instance — built once on module import
_graph = build_graph()


def build_input(history: list[dict], message: str) -> dict:
    """Convert the C# ChatLog format into LangGraph MessagesState input."""
    messages = []
    for h in history:
        role = h.get("role", "user")
        content = h.get("content", "")
        if role == "user":
            messages.append(HumanMessage(content=content))
        else:
            messages.append(AIMessage(content=content))
    messages.append(HumanMessage(content=message))
    return {"messages": messages}


async def stream_chat(history: list[dict], message: str) -> AsyncIterator[dict]:
    """
    Stream chat tokens and metadata events back to the caller.
    Yields dicts with either {"token": str} or {"meta": {...}}
    """
    graph_input = build_input(history, message)

    redirect = None
    full_response = []

    try:
        async for event in _graph.astream_events(graph_input, version="v2"):
            event_type = event.get("event")

            # Stream tokens from the LLM
            if event_type == "on_chat_model_stream":
                chunk = event["data"]["chunk"]
                token = chunk.content if hasattr(chunk, "content") else ""
                if token:
                    full_response.append(token)
                    yield {"token": token}

            # Intercept tool results for special sentinel values
            elif event_type == "on_tool_end":
                output = event["data"].get("output", "")
                if hasattr(output, "content"):
                    output = output.content
                logger.info("tool_completed", tool=event.get("name"), output=output)
                if isinstance(output, str) and output.startswith("__REDIRECT__"):
                    redirect = output.replace("__REDIRECT__", "").replace("__", "")

    except Exception as e:
        logger.error("samuellm_stream_failed", error=str(e))
        yield {"token": "I encountered an error. Please try again."}
        yield {"meta": {"error": True}}
        return
    
    logger.info("stream_complete", full_response_length=len(full_response), tokens_collected="".join(full_response)[:50])

    yield {
        "meta": {
            "redirect_to_page": redirect,
            "full_response": "".join(full_response),
        }
    }


async def query_chat(history: list[dict], message: str) -> dict:
    """Non-streaming version for background agents that need a complete response."""
    graph_input = build_input(history, message)
    result = await _graph.ainvoke(graph_input)
    last_message = result["messages"][-1]
    return {"response": last_message.content}
