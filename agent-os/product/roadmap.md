# Product Roadmap

1. [ ] Document ingestion pipeline — Ingest files and docs, extract text, and normalize it
   into structured facts with source metadata and timestamps. `[M]`
2. [ ] Intent classification and clarification — Classify requests as new knowledge,
   retrieval, or update, and prompt for confirmation when updates are detected. `[S]`
3. [ ] Knowledge store foundation — Persist facts in PostgreSQL and embeddings in Qdrant
   with CRUD APIs and basic version history. `[M]`
4. [ ] Grounded retrieval API — Retrieve relevant facts with citations and return
   consistent responses constrained to stored knowledge. `[M]`
5. [ ] Task execution runner — Execute requested actions using retrieved facts, with
   safeguards and clear success/failure responses. `[M]`
6. [ ] Conflict resolution workflow — Detect conflicting facts, surface discrepancies,
   and resolve to a single trusted version with provenance. `[M]`
7. [ ] Documentation connectors — Add scheduled imports for common corp sources
   (repositories, shared drives, wikis) with incremental updates. `[L]`
8. [ ] Governance and quality — Role-based access control, audit logging, and an eval
   suite for retrieval accuracy and action safety. `[L]`

> Notes
> - Order items by technical dependencies and product architecture
> - Each item should represent an end-to-end (frontend + backend) functional and testable feature
