from app.llm import ChatResult
from app import llm
from app.rag.retrieval import Hit, _keyword_terms, rerank, reciprocal_rank_fusion

from conftest import ScriptedLlm


def hit(i: int, similarity: float | None = None) -> Hit:
    return Hit(id=i, document_id="d", document_name="doc", page=1, content=f"chunk {i}", score=0.0, similarity=similarity)


def test_rrf_prefers_chunks_both_lists_agree_on():
    vector = [hit(1), hit(2), hit(3)]
    keyword = [hit(3), hit(4), hit(1)]

    fused = reciprocal_rank_fusion([vector, keyword])

    assert [h.id for h in fused][:2] == [1, 3]  # both lists contain 1 and 3
    assert {h.id for h in fused} == {1, 2, 3, 4}


def test_rrf_keeps_cosine_similarity_from_vector_list():
    fused = reciprocal_rank_fusion([[hit(1)], [hit(1, similarity=0.8)]])
    assert fused[0].similarity == 0.8


def test_keyword_terms_drop_stopwords_and_short_words():
    assert _keyword_terms("What is the maximum size of a file?") == ["maximum", "size", "file"]


def test_rerank_reorders_by_model_answer():
    llm.set_llm(ScriptedLlm("[2, 0, 1]"))
    out = rerank("q", [hit(0), hit(1), hit(2)], top_k=2)
    assert [h.id for h in out] == [2, 0]


def test_rerank_falls_back_to_input_order_on_garbage():
    llm.set_llm(ScriptedLlm("I think chunk two is best"))
    out = rerank("q", [hit(0), hit(1), hit(2)], top_k=2)
    assert [h.id for h in out] == [0, 1]


def test_rerank_survives_model_failure():
    class Broken:
        def complete(self, *a, **k):
            raise RuntimeError("provider down")

    llm.set_llm(Broken())
    out = rerank("q", [hit(0), hit(1)], top_k=2)
    assert [h.id for h in out] == [0, 1]
