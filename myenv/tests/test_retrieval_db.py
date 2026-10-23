"""Integration tests against a real pgvector Postgres.

    docker run -d --name ff-pgvector -e POSTGRES_PASSWORD=test -p 5433:5432 pgvector/pgvector:pg16
    $env:TEST_DATABASE_URL = "postgresql://postgres:test@localhost:5433/postgres"
    pytest
"""

import uuid

from conftest import requires_db

from app.rag.ingest import delete_document, ingest_document
from app.rag.retrieval import keyword_search, search, vector_search
from app import embeddings

pytestmark = requires_db


def add(tenant, name, text):
    doc = uuid.uuid4()
    ingest_document(tenant_id=tenant, document_id=doc, document_name=name, data=text.encode(), content_type="text/plain")
    return doc


def test_answer_chunk_is_found_and_cited_with_page(db, tenant_a):
    add(tenant_a, "handbook.md", "Intro to the team and its goals.\n---page---\nThe maximum attachment size is 25 MB each.")
    add(tenant_a, "other.md", "Completely unrelated text about lunch menus and coffee.")

    hits = search(tenant_a, "what is the maximum attachment size", mode="vector", top_k=2)

    assert hits[0].document_name == "handbook.md"
    assert hits[0].page == 2


def test_a_second_tenant_cannot_see_the_first_tenants_documents(db, tenant_a, tenant_b):
    add(tenant_a, "secret.md", "The launch codename is Falcon and the budget is large.")

    assert search(tenant_b, "launch codename Falcon", mode="vector") == []
    assert search(tenant_b, "launch codename Falcon", mode="hybrid") == []
    assert keyword_search(tenant_b, "Falcon", 5) == []
    assert len(search(tenant_a, "launch codename Falcon", mode="hybrid")) == 1


def test_reingesting_replaces_chunks_instead_of_duplicating(db, tenant_a):
    doc = add(tenant_a, "a.md", "alpha beta gamma")
    ingest_document(tenant_id=tenant_a, document_id=doc, document_name="a.md", data=b"alpha beta gamma", content_type="text/plain")

    with db.connection() as conn:
        count = conn.execute("SELECT count(*) FROM chunks WHERE document_id = %s", (doc,)).fetchone()[0]
    assert count == 1


def test_delete_removes_only_that_tenants_chunks(db, tenant_a, tenant_b):
    doc = add(tenant_a, "a.md", "alpha beta gamma")

    assert delete_document(tenant_b, doc) == 0  # wrong tenant deletes nothing
    assert delete_document(tenant_a, doc) == 1


def test_keyword_search_finds_exact_terms_vectors_can_miss(db, tenant_a):
    add(tenant_a, "ids.md", "Ticket FF-4821 was escalated by support.")
    hits = keyword_search(tenant_a, "FF 4821 escalated", 5)
    assert hits and hits[0].document_name == "ids.md"


def test_hybrid_search_returns_vector_similarity(db, tenant_a):
    add(tenant_a, "a.md", "sprints have a planning active completed and cancelled status")
    hits = search(tenant_a, "what statuses can a sprint have", mode="hybrid", top_k=3)
    assert hits and hits[0].similarity is not None


def test_embedder_dimension_matches_schema(db):
    assert len(embeddings.embed_text("hello")) == 1536
