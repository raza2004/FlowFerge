# FlowForge

Multi-tenant, AI-augmented project management SaaS.

## Layout
- `backend/` .NET 8 Clean Architecture (Domain, Application, Infrastructure, API, Workers). CQRS with MediatR, EF Core + PostgreSQL, SignalR, MassTransit + RabbitMQ.
- `frontend/flowforge-web/` Angular 21 web app (standalone components, signals, Tailwind 3, Angular Material).
- `frontend/flowforge-admin/` Angular admin portal (feature flags, tenants, users, audit).
- `myenv/` Python FastAPI AI service (RAG, agent). This folder is also the Python virtual environment; only project files are committed.

## Working rules
- Secrets only in `.env` (gitignored) and `dotnet user-secrets`. Never in tracked files.
- Never trigger GitHub Actions. CI is `workflow_dispatch` only while the project is in development.
- Do not start servers or Docker unless asked. Give commands instead.
- No dash characters in visible UI text.
- Do not invent features or numbers. Use real data or say it is missing.

## Design system: "Ember Editorial"
Use the `distinctive-ui` skill. Tokens live in `frontend/flowforge-web/tailwind.config.js` and `src/styles.scss`; the admin app has its own `tailwind.config.js` with the same values.
- Colours: `canvas` paper, `panel` raised paper, `sunken`, `line`, `ink` (DEFAULT/soft/muted/faint), `ember` accent, `tone.*` for status only.
- Fonts: Instrument Serif (display, `font-display`), Geist (UI, `font-sans`), Geist Mono (numbers and ids, `font-mono`).
- Primary buttons are ink black; ember is for accents, active states and focus. Hairline borders, small radii, no gradient text, no glow blobs.
- Motion: transform and opacity only, 150 to 500 ms, honour reduced motion. GSAP + Lenis only on the marketing page.

## Commands
- Backend: `cd backend && dotnet test FlowForge.sln`
- Web: `cd frontend/flowforge-web && npx ng build`
- AI service tests: `cd myenv && ./Scripts/python.exe -m pytest` (set `TEST_DATABASE_URL` for the pgvector tests)
