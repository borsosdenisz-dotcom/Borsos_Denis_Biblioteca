# Integrare MCP, RAG și Embeddings

## Context

Integrarea a fost realizată pe branch-ul `integrare-mcp-rag`, păstrând proiectul local drept proiect principal. Sursa de comparație a fost `georgesabau/Biblioteca-MCP-RAG`, clonată separat, în directorul temporar al sistemului. Repository-ul sursă nu a fost modificat.

## Fișiere și funcționalități integrate

- `Biblioteca.Embeddings/`: proiect consolă nou, catalogul `books.csv`, embeddings cu `embeddinggemma` sau modelul indicat de `OLLAMA_EMBEDDING_MODEL`, similaritate cosinus și căutare semantică în 40 de cărți.
- `Lab1_MVC/Docs/regulament.md`: regulamentul cu 13 secțiuni.
- `Lab1_MVC/Rag/`: chunking, index vectorial în memorie și căutare hibridă semantică/lexicală.
- `Lab1_MVC/Mcp/DocumentTools.cs`: tool-ul MCP `search_documents`, cu sursele `regulament`, `carte` și `ambele`, plus `top` între 1 și 8.
- `Biblioteca.Agent/Program.cs`: inspector MCP, agent Ollama `llama3.2`, afișarea apelurilor `[tool]`, căutare directă cu fragmente/scoruri și mesaje pentru erori de conexiune.
- `Lab1_MVC/Api/DevelopmentEndpoints.cs`: izolează încărcarea Scalar, astfel încât o politică Windows Application Control să nu oprească aplicația.
- `Lab1_MVC/Program.cs` și `appsettings.json`: înregistrarea serviciilor RAG/Ollama, indexare la pornire și configurare fără credențiale în cod.
- `Lab1_MVC.sln`: include acum `Biblioteca.Embeddings`.
- `GenreClassifier.Training/Program.cs`: acceptă căile input/output ca argumente, fără a crea un proiect de training duplicat.

Baza SQLite existentă nu a fost înlocuită și nu este inclusă în commit. Două descrieri lipsă din baza locală sunt completate numai în indexul din memorie, din `books.csv`; baza nu este rescrisă.

## Funcționalități păstrate

Au rămas neschimbate aplicația MVC, CRUD-urile, modelele și migrările existente, recomandările ML.NET, clasificarea genurilor, serviciul gRPC, clientul gRPC și tool-urile MCP anterioare (`search_books`, `get_book_details`, `recommend_books`, `top_books_by_price`, `books_by_genre`). Nu a fost necesară o migrare EF Core nouă.

## Verificări efectuate

- `dotnet restore Lab1_MVC.sln`: reușit.
- `dotnet build Lab1_MVC.sln --no-restore`: reușit pentru toate proiectele.
- `dotnet test Lab1_MVC.sln --no-build`: reușit; soluția nu conține încă proiecte de teste automate.
- Pornire MVC pe `https://localhost:7105` și `http://localhost:5114`: reușită.
- Index inițial: 13 secțiuni din regulament și 40 de cărți.
- MVC `/Books`: HTTP 200.
- REST recomandări: HTTP 200.
- gRPC Recommender unary pentru cititorul 5: reușit, 3 recomandări primite.
- MCP inspector și apeluri `search_books`/`search_documents`: reușite.
- `embeddinggemma`, `nomic-embed-text` și `llama3.2`: disponibile și testate local.
- Cele șase întrebări RAG cerute au recuperat pe primul loc secțiunile/cartea corectă în test interactiv UTF-8.

## Pornire din VS Code

1. Deschide rădăcina repository-ului și un terminal PowerShell.
2. Pornește Ollama și descarcă modelele, dacă lipsesc:

   ```powershell
   ollama serve
   ollama pull embeddinggemma
   ollama pull llama3.2
   ```

3. Restaurează și compilează:

   ```powershell
   dotnet restore Lab1_MVC.sln
   dotnet build Lab1_MVC.sln
   ```

4. Pornește aplicația și serverul MCP:

   ```powershell
   dotnet run --project Lab1_MVC --launch-profile https
   ```

5. În alt terminal, pornește Agent-ul:

   ```powershell
   dotnet run --project Biblioteca.Agent
   ```

6. Pentru embeddings:

   ```powershell
   dotnet run --project Biblioteca.Embeddings
   ```

7. Pentru clientul gRPC, cu aplicația MVC pornită:

   ```powershell
   dotnet run --project Biblioteca.Client
   ```

Endpoint-ul MCP implicit este `https://localhost:7105/mcp`. Poate fi schimbat pentru Agent prin `BIBLIOTECA_MCP_ENDPOINT`. Endpoint-ul Ollama este `http://localhost:11434`; Agent-ul acceptă `OLLAMA_OPENAI_ENDPOINT` și `OLLAMA_CHAT_MODEL`, iar Embeddings acceptă `OLLAMA_ENDPOINT` și `OLLAMA_EMBEDDING_MODEL`.

## Probleme rămase

- Politica Windows Application Control locală blochează `Scalar.AspNetCore.dll`. Aplicația continuă să pornească, iar OpenAPI rămâne disponibil; doar UI-ul Scalar este omis în acel mediu.
- Restore/build raportează patru avertismente de vulnerabilități tranzitive NuGet și două avertismente nullable preexistente în view-urile `Books/Delete` și `Books/Details`.
- Endpoint-urile GitHub Models necesită secretul `GitHubModels:Token`; secretul nu este stocat în repository. MCP, RAG, MVC și gRPC nu depind de el.
- Nu s-a făcut push și nu s-a făcut merge în `main`.
