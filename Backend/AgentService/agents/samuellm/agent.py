from typing import AsyncIterator
from langchain_openai import AzureChatOpenAI
from langchain_core.messages import HumanMessage, AIMessage, SystemMessage
from langgraph.graph import StateGraph, MessagesState, END
from langgraph.prebuilt import ToolNode, tools_condition
from agents.samuellm.prompts import SYSTEM_PROMPT
from tools.retrieval import search_experience
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


def _build_graph():
    llm = _build_llm()
    llm_with_tools = llm.bind_tools(TOOLS)

    def agent_node(state: MessagesState):
        # Prepend system prompt on every call
        messages = [SystemMessage(content=SYSTEM_PROMPT)] + state["messages"]
        response = llm_with_tools.invoke(messages)
        return {"messages": [response]}

    tool_node = ToolNode(TOOLS)

    workflow = StateGraph(MessagesState)
    workflow.add_node("agent", agent_node)
    workflow.add_node("tools", tool_node)
    workflow.set_entry_point("agent")
    workflow.add_conditional_edges("agent", tools_condition)
    workflow.add_edge("tools", "agent")

    return workflow.compile()


# Single graph instance — built once on module import
_graph = _build_graph()


def _build_input(history: list[dict], message: str) -> dict:
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
    graph_input = _build_input(history, message)

    redirect = None
    display_resume = False
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
                if isinstance(output, str):
                    if output.startswith("__REDIRECT__"):
                        redirect = output.replace("__REDIRECT__", "").replace("__", "")
                    elif output == "__DISPLAY_RESUME__":
                        display_resume = True

    except Exception as e:
        logger.error("samuellm_stream_failed", error=str(e))
        yield {"token": "I encountered an error. Please try again."}
        yield {"meta": {"error": True}}
        return
    
    logger.info("stream_complete", full_response_length=len(full_response), tokens_collected="".join(full_response)[:50])

    yield {
        "meta": {
            "redirect_to_page": redirect,
            "display_resume": display_resume,
            "full_response": "".join(full_response),
        }
    }


async def query_chat(history: list[dict], message: str) -> dict:
    """Non-streaming version for background agents that need a complete response."""
    graph_input = _build_input(history, message)
    result = await _graph.ainvoke(graph_input)
    last_message = result["messages"][-1]
    return {"response": last_message.content}
