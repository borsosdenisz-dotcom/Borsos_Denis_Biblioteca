# Laboratorul Î4 · Borsos Denis · nr. matricol 121200052 · varianta A2 · B1

|Parametru|Valoarea mea|
|-|-|
|A (ultima cifră a nr. matricol: 2)|A2|
|B (penultima cifră a nr. matricol: 5)|B1|
|V1 · cărți pe pagină|7|
|V2 · sortarea implicită a catalogului|preț desc.|
|V3 · filtrul din S3|an de la–până la|
|V4 · indicatorul din S2|preț mediu|

## S1 · Comenzile

* Protecția la overposting aleasă: **ViewModel** (`OrderFormViewModel`: OrderID, CustomerID, BookID, OrderDate).
* De ce am ales-o: Create și Edit primesc `OrderFormViewModel`, nu entitatea `Order`, deci model binding-ul nu poate lega nimic în afara acestor patru câmpuri (nici navigările `Customer`/`Book`, nici câmpuri adăugate mai târziu în entitate). La Create ignor `OrderID` și construiesc o entitate nouă; la Edit încarc comanda din bază și copiez doar câmpurile permise. Spre deosebire de `[Bind]`, lista câmpurilor permise e verificată de compilator și nu e un șir de text; ViewModel-ul îmi dă și locul pentru validare (`[Range]` pe cititor și carte) și pentru data implicită (azi).
* Comenzi adăugate din interfață (formularul Orders/Create, cu token antiforgery), pe 2026-11-05: **#301 Cititor 3 → Dune** și **#302 Cititor 7 → Solaris**. Ambele apar pe /Orders, primele în listă (sortare descrescătoare după dată), cu numele cititorului și titlul cărții.

## S2 · Pagina unui autor și sortarea după autor

* Indicatorul V4 (prețul mediu al cărților autorului) este calculat cu interogarea:

```csharp
var authorBooks = _context.Book.Where(b => b.AuthorID == id);
var averagePrice = await authorBooks.AverageAsync(b => (decimal?)b.Price);
```

SQL-ul real generat de EF Core (din logul `Microsoft.EntityFrameworkCore.Database.Command`):

```sql
SELECT ef_avg("b"."Price")
FROM "Book" AS "b"
WHERE "b"."AuthorID" = @id
```

`Price` e `decimal`, stocat în SQLite ca TEXT. EF Core 10 nu folosește `AVG` simplu, ci funcția `ef_avg`, pe care o înregistrează pe conexiunea SQLite și care calculează media cu precizie zecimală. Calculul rămâne în motorul bazei de date: în C# vine o singură valoare, nu lista de cărți. Verificare: pentru Liviu Rebreanu (id 4) pagina afișează 29.50, iar `SELECT avg(Price) FROM Book WHERE AuthorID=4` dă tot 29.5 (Ion 20, Pădurea spânzuraților 39).

Totalul comenzilor se calculează tot în SQL:

```sql
SELECT COUNT(*)
FROM "Order" AS "o"
INNER JOIN "Book" AS "b" ON "o"."BookID" = "b"."ID"
WHERE "b"."AuthorID" = @id
```

* De ce `OrderBy(b => b.Author.FullName)` aruncă o excepție? `FullName` e o proprietate C# calculată (`public string FullName => $"{FirstName} {LastName}";`), fără setter. EF o tratează ca nemapată (ca și cum ar avea `[NotMapped]`), deci nu are coloană în tabela `Author`. EF trebuie să traducă tot `OrderBy` în SQL și nu are în ce coloană să traducă `FullName`. Excepția reală, obținută pe `/Books?sortOrder=author`:

```
System.InvalidOperationException: The LINQ expression 'DbSet<Book>()
    .LeftJoin(inner: DbSet<Author>(), ...)
    .OrderBy(b => b.Inner.FullName)' could not be translated. Additional information:
Translation of member 'FullName' on entity type 'Author' failed. This commonly occurs
when the specified member is unmapped. Either rewrite the query in a form that can be
translated, or switch to client evaluation explicitly by inserting a call to 'AsEnumerable',
'AsAsyncEnumerable', 'ToList', or 'ToListAsync'.
```

* Cum am rezolvat: am sortat după coloanele reale, `OrderBy(b => b.Author!.LastName).ThenBy(b => b.Author!.FirstName)` (și `OrderByDescending`/`ThenByDescending` pentru `author_desc`). În SQL se generează `ORDER BY "a"."LastName", "a"."FirstName"`, înainte de `LIMIT/OFFSET`. Nu am folosit `AsEnumerable()`/`ToList()`, cum sugerează mesajul, pentru că sortarea și paginarea s-ar fi făcut atunci în memorie, după ce s-ar fi încărcat tot catalogul.

## S3 · Filtrul variantei mele (V3)

* Filtrul implementat: **an, de la – până la** (`yearFrom` / `yearTo`, pe `Book.Year`). Sunt două câmpuri numerice, „An de la” și „An până la”, în același formular cu căutarea după titlu. Fiecare capăt al intervalului e opțional.
* Interogarea, cu filtrul aplicat (`rows.ToQueryString()`, scrisă în log la nivel Debug; scenariul de mai jos):

```sql
.param set @searchString 'a'
.param set @yearFrom 1900
.param set @yearTo 1970

SELECT "b"."ID", "b"."Title", CASE
    WHEN "a"."ID" IS NOT NULL THEN COALESCE("a"."FirstName", '') || ' ' || COALESCE("a"."LastName", '')
    ELSE ''
END, CASE
    WHEN "g"."ID" IS NOT NULL THEN "g"."Name"
    ELSE ''
END, "b"."Price", (
    SELECT COUNT(*)
    FROM "Order" AS "o"
    WHERE "b"."ID" = "o"."BookID")
FROM "Book" AS "b"
LEFT JOIN "Author" AS "a" ON "b"."AuthorID" = "a"."ID"
LEFT JOIN "Genre" AS "g" ON "b"."GenreID" = "g"."ID"
WHERE instr("b"."Title", @searchString) > 0 AND "b"."Year" >= @yearFrom AND "b"."Year" <= @yearTo
ORDER BY "b"."Title"
```

`PaginatedList.CreateAsync` rulează pe același `IQueryable` un `SELECT COUNT(*) ... WHERE ...` și apoi interogarea paginii, cu `ORDER BY "b"."Title" LIMIT @p OFFSET @p` (vezi logul `Database.Command`). Filtrarea, sortarea și paginarea se fac toate în SQLite.

* Parametrii păstrați: link-urile de sortare (Titlu, Autor, Preț) și butoanele Înapoi/Înainte trimit `sortOrder`, `currentFilter`, `yearFrom`, `yearTo` (plus `pageNumber` la paginare). Formularul are `sortOrder` într-un câmp ascuns și nu trimite `pageNumber`, deci orice schimbare a căutării sau a intervalului de ani pornește de la pagina 1. În plus, rămâne regula din PDF: `if (searchString != null) pageNumber = 1;`.
* Scenariul testat: am căutat **„a”** în titlu, cu anul **de la 1900 până la 1970** și sortare **după titlu, crescător** (`/Books?sortOrder=title&searchString=a&yearFrom=1900&yearTo=1970`). Au ieșit **10 cărți**, adică **2 pagini** (7 + 3, V1 = 7). Pagina 1: Craii de Curtea-Veche … Pădurea spânzuraților. Pagina 2 (prin „Înainte”, URL `?sortOrder=title&pageNumber=2&currentFilter=a&yearFrom=1900&yearTo=1970`): Solaris, Ultima noapte de dragoste…, Un vrăjitor din Terramare. Pe pagina 2, link-urile de sortare și „Înapoi” au păstrat toți cei patru parametri. Am verificat numărul și direct în bază: `SELECT count(*) FROM Book WHERE instr(Title,'a')>0 AND Year BETWEEN 1900 AND 1970` → 10.
* Observație: pe SQLite, `Contains` devine `instr(...)`, care ține cont de majuscule. Ordinea titlurilor e binară, de aceea „Câinele din Baskerville” apare după „Crima din Orient Express”.

## S4 · Folosirea AI

* **Instrumente folosite:** Claude Code (agent în VS Code), care a rulat comenzile `dotnet`, `git` și `gh` și a editat fișierele. Eu am dat cerințele și datele variantei și am verificat rezultatele (paginile cu `curl`, SQL-ul din log, valorile direct în bază).
* **La ce pași:** la toți: A1–A5 (pornire, scaffolding cu `dotnet aspnet-codegenerator`, proiecție, paginare, detalii), S1–S3 și redactarea jurnalului. Fiecare pas a fost compilat, rulat și verificat, apoi am făcut commit separat.
* **Un exemplu de cerere trimisă:**

> S3: pe Books/Index, două câmpuri „An de la” / „An până la”, combinate cu căutarea și sortarea. Toate link-urile de sortare și butoanele Înapoi/Înainte trebuie să păstreze TOȚI parametrii activi (sortOrder, currentFilter, yearFrom, yearTo). Schimbarea filtrului resetează pagina la 1. Găsește și testează un scenariu cu cel puțin 2 pagini (≥ 8 rezultate).

* **O situație în care am corectat sau respins un răspuns AI:**

  * Ce a propus AI-ul / care era eroarea: pentru sortarea după autor (S2), prima variantă scrisă a fost `OrderBy(b => b.Author!.FullName)`. La rulare, `/Books?sortOrder=author` a dat HTTP 500 cu `InvalidOperationException: ... Translation of member 'FullName' on entity type 'Author' failed`. Mesajul EF propune „switch to client evaluation ... 'AsEnumerable' ... 'ToList'”. Asta ar fi fost a doua greșeală: sortarea s-ar fi făcut în memorie, după încărcarea întregului catalog, și ar fi stricat paginarea în SQL (`LIMIT/OFFSET`).
  * Cum mi-am dat seama că e greșit sau ineficient: din codul 500 și din excepția din log. `FullName` e o proprietate calculată în C#, fără coloană în bază. Am respins sugestia cu `ToList()` pentru că cerința e ca sortarea și paginarea să rămână în SQL. După corectare am verificat în log că apare `ORDER BY "a"."LastName", "a"."FirstName"` înainte de `LIMIT @p OFFSET @p`.
  * Codul inițial:

```csharp
"author"      => books.OrderBy(b => b.Author!.FullName),
"author_desc" => books.OrderByDescending(b => b.Author!.FullName),
```

* Codul final:

```csharp
"author"      => books.OrderBy(b => b.Author!.LastName).ThenBy(b => b.Author!.FirstName),
"author_desc" => books.OrderByDescending(b => b.Author!.LastName).ThenByDescending(b => b.Author!.FirstName),
```

* **A doua greșeală reală a AI-ului (S3):** voia să scrie SQL-ul în log cu `ToQueryString()`, dar a editat fișierul printr-un script Python, iar `\n` din șir a ajuns în cod ca o linie nouă reală. `dotnet build` a dat `error CS1010: Newline in constant` și `CS1002: ; expected` la `BooksController.cs(71)`. Cod inițial (generat greșit):

```csharp
_logger.LogDebug("SQL catalog:
{Sql}", rows.ToQueryString());
```

Cod final:

```csharp
if (_logger.IsEnabled(LogLevel.Debug))
    _logger.LogDebug("SQL catalog:\n{Sql}", rows.ToQueryString());
```

* **O presupunere care nu s-a confirmat:** mă așteptam ca SQLite să nu poată face `ORDER BY` / `AVG` pe `decimal` (eroare cunoscută la versiuni mai vechi de EF Core). Cu EF Core 10 nu a apărut nicio excepție. Sortarea se traduce în `ORDER BY "b"."Price" COLLATE EF_DECIMAL DESC`, iar media în `SELECT ef_avg("b"."Price")`, deci ambele rămân în SQL. Nu a fost nevoie de `HasConversion<double>()` sau de migrație, așa că nu am schimbat modelul.


## Laboratorul 5 · Borsos Denis · 121200052 · A2 · B1

Varianta: **A2** → pragul pentru cold start = **10 evaluări**; **B1** → al doilea grafic din dashboard = **media evaluărilor pe autor**.

### A1 · Evaluările în baza de date

* Am importat `Lab1_MVC/Data/book-ratings.csv` **o singură dată**, prin formularul din `/Admin` (POST multipart pe `/Admin/ImportRatings`, cu tokenul antiforgery). Mesajul afișat: **„Au fost importate 6599 evaluări.”**
* Verificare în bază: `SELECT COUNT(*) FROM Rating` = **6599** (cât rânduri de date are CSV-ul), **300** de cititori distincți, note între 1 și 5. Perechi (ReaderId, BookID) duplicate: **0**.

### A2 · Reantrenarea din aplicație

* `Services/RecommenderTrainer` (înregistrat `AddScoped`) citește evaluările din tabelul `Rating` și suprascrie `MLModels/BookRecommender.zip`.
* Mesajul după „Reantrenează”: **„Model reantrenat pe 6599 evaluări. RMSE = 0.71”** (la o nouă apăsare: RMSE = 0.70). Valoarea variază puțin între rulări, chiar cu `seed: 1`, pentru că Matrix Factorization se antrenează pe mai multe fire de execuție.
* **`/Recommendations?userId=5` a mers fără repornirea aplicației.** În log apare `Reloading file 'MLModels/BookRecommender.zip' completed` (efectul lui `watchForChanges: true`), iar scorurile s-au schimbat față de modelul vechi (de exemplu, „Domain-Driven Design” 4.58 → 4.50, „C# in Depth” 4.11 → 3.93).

### A3 · API de recomandări cu cold start

* `GET /api/v2/readers/{id}/recommendations?top=N` (implicit 5, între 1 și 20). Pragul de cold start este `const int minRatings = 10; // pragul variantei A2 = 10`.
* **Cititorul 5** (are 23 de cărți evaluate, care sunt excluse) primește recomandări **personalizate**, prezise de model, toate cu `"fallback": false`: Designing Data-Intensive Applications (4.08), Fata cu un tatuaj de dragon (3.87), Moromeții (2.93), Numele vântului (2.92), Pădurea spânzuraților (2.67). Cu `?top=3` primește doar primele 3.
* **Cititorul 999** nu există în date, deci modelul întoarce `NaN` pentru toate cărțile. Se aplică cold start-ul și primește aceeași listă pe care ar primi-o orice cititor nou: **cele mai bine evaluate cărți care au ≥ 10 evaluări**, cu `"fallback": true`, iar scorul este **media reală a notelor**, nu o predicție: Câinele din Baskerville (4.14, 179 evaluări), Domain-Driven Design (3.81), Fata cu un tatuaj de dragon (3.81), Dune (3.74), Istoria critică a românilor (3.72). Am verificat mediile cu un `GROUP BY BookID HAVING COUNT(*) >= 10` direct în bază și sunt identice.

### A4 · Clasificarea genului din descriere

* Proiect nou `GenreClassifier.Training` (în soluție), cu `book-descriptions.csv`: 1270 de exemple, 9 genuri; Poezie (112) și Nuvelă (110) sunt clasele cele mai mici, celelalte au 142–152 de exemple.
* **MicroAccuracy = 75.1%, MacroAccuracy = 74.9%.**
* **De ce diferă:** Micro este media pe toate exemplele din test (fiecare carte contează la fel), iar Macro este media acurateților pe clase (fiecare gen contează la fel). Clasele mici Poezie (recall 0.64) și Nuvelă (0.625) au cea mai slabă acuratețe și trag Macro în jos. La mine diferența e foarte mică (0.2 puncte), pentru că setul de test e aproape echilibrat, iar o clasă mare, Programare, are și ea un recall slab (0.62), ceea ce scade în aceeași măsură și Micro.
* **Matricea de confuzie, cele mai dese confuzii:**
  * **Poezie ↔ Nuvelă**: 6 poezii prezise ca nuvele și 5 nuvele prezise ca poezii, deci 11 în total, cea mai mare confuzie;
  * **Programare ↔ SF**: 5 + 4 = 9;
  * **Programare → Istorie și eseu**: 6;
  * **Clasic universal → Roman românesc**: 6.

  Este logic: poezia și nuvela sunt ambele genuri literare scurte, cu vocabular asemănător; descrierile de programare și cele SF folosesc aceleași cuvinte („cod”, „hacker”, „rețea”, „testarea automată”, chiar și în descrieri SF din CSV); iar clasicii universali și romanele românești sunt descriși la fel (romane, personaje, epocă).
* **Dune** („Pe o planetă deșertică, controlul unei resurse rare decide soarta unui imperiu.”) → **SF, 74% încredere**. A ghicit corect.
* **Descrierea ambiguă** („un detectiv pe o stație spațială”) → **SF, 79% încredere**. Contrar așteptării, încrederea nu a scăzut. Modelul a ales SF cu încredere mare, probabil pentru că în CSV „stați…” apare în 34 de descrieri SF și „spațial” în 19, în timp ce „detectiv” apare în doar 11 descrieri (toate Polițist). Cuvintele „spațiale” cântăresc mai mult decât „detectiv”. Asta arată că „încrederea” este doar scorul maxim al softmax-ului, nu o măsură a ambiguității reale.
* Integrare: `MLModels/GenreClassifier.zip` (Copy if newer), `Models/BookText` și `Models/GenrePrediction`, al doilea `PredictionEnginePool`, acțiunea `POST /Books/SuggestGenre` (JSON; descrierea goală dă 400) și butonul „Sugerează genul” în `Books/Create`. View-ul `Create` nu avea câmpul Description, așa că l-am adăugat (textarea) și am inclus `Description` în `[Bind]`. Scriptul este pus în secțiunea `@section Scripts`, care exista deja.

### S1 · Jurnalul recomandărilor

* Entitatea `RecommendationLog` (ID, ReaderId, CreatedAt în UTC, BookIds ca text, de exemplu „30,27,5”) și migrația `RecommendationLog`. La fiecare apel al API-ului de recomandări se salvează un rând, **inclusiv la cold start** (de exemplu, cititorul 999 → „25,32,27,16,35”).
* Pagina `/Admin/Log` (link „Jurnal recomandări” în `/Admin`) conține un tabel paginat cu `PaginatedList` (10 pe pagină), un filtru după cititor (`readerId`), un interval de date „de la”–„până la” (ziua „până la” este inclusă complet) și sortare după dată (implicit descrescător, cu click pe antet devine crescător). Filtrele, sortarea și paginarea se fac în SQL. Link-urile de sortare și paginare păstrează toți parametrii; formularul de filtrare nu trimite `pageNumber`, deci orice schimbare de filtru pornește de la pagina 1.
* Test: 23 de apeluri → 3 pagini; `readerId=5` → 6 rânduri; `readerId=999` → 3; intervalul de azi → toate; `to=ieri` / `from=mâine` → 0.

### S2 · Dashboard

* Pagina `/Admin/Dashboard` (link în `/Admin`), cu Chart.js din CDN. Carduri: **6.599 evaluări, 300 de cititori distincți, media generală 3,25**. Grafic cu bare pentru distribuția notelor: 1★ = 365, 2★ = 1179, 3★ = 2361, 4★ = 1854, 5★ = 840. Al doilea grafic, cu bare orizontale: **media evaluărilor pe autor** pentru 36 de autori, de la Arthur Conan Doyle (4,14) la Robert C. Martin (2,26). Sub grafice există și un tabel cu aceleași date.
* Toate valorile coincid cu interogări directe în SQLite (`COUNT`, `COUNT(DISTINCT ReaderId)`, `AVG`, `GROUP BY Stars`, `GROUP BY` pe autor).
* Interogarea reală pentru media pe autor (capturată cu `ToQueryString()` și confirmată în logul EF `Executed DbCommand`):

```sql
SELECT "s"."Key", AVG(CAST("s"."Stars" AS REAL)), COUNT(*)
FROM (
    SELECT "r"."Stars", COALESCE("a"."FirstName", '') || ' ' || COALESCE("a"."LastName", '') AS "Key"
    FROM "Rating" AS "r"
    INNER JOIN "Book" AS "b" ON "r"."BookID" = "b"."ID"
    LEFT JOIN "Author" AS "a" ON "b"."AuthorID" = "a"."ID"
    WHERE "a"."ID" IS NOT NULL
) AS "s"
GROUP BY "s"."Key"
ORDER BY AVG(CAST("s"."Stars" AS REAL)) DESC
```

### Folosirea AI (Laboratorul 5)

* **Instrument:** Claude Code (agent în VS Code), la toți pașii: A1–A4, S1, S2 și redactarea jurnalului. Fiecare pas a fost compilat (`dotnet build`), aplicația a fost pornită și verificată cu `curl -k` (inclusiv formularele, cu token antiforgery și cookie), cifrele au fost comparate cu interogări directe în bază, apoi am făcut un commit separat și push.
* **Un exemplu de cerere:**

> S2: pagina Admin/Dashboard cu Chart.js (CDN): carduri cu numărul de evaluări, numărul de cititori distincți și media generală; grafic cu bare pentru distribuția notelor 1–5; al doilea grafic, cu bare, pentru MEDIA EVALUĂRILOR PE AUTOR (B1): GroupBy pe autor (nume complet din FirstName + LastName, nu FullName), media Stars, ordonat descrescător, calculat în SQL.

* **Problemă reală (S2):** prima variantă a interogării pe autor proiecta direct în record-ul `AuthorAverage` și apoi ordona după `x.Average`. `/Admin/Dashboard` a dat **HTTP 500**: `InvalidOperationException: The LINQ expression ... .OrderByDescending(e0 => e0.Average) could not be translated`. EF nu poate traduce în SQL un membru al unui obiect creat prin constructor. Sugestia din mesaj (`AsEnumerable`/`ToList`) ar fi mutat gruparea în memorie, deci am respins-o. Am făcut o proiecție anonimă, ordonarea în SQL, apoi conversia la record.

  Cod inițial:

```csharp
.GroupBy(r => r.Book.Author!.FirstName + " " + r.Book.Author!.LastName)
.Select(g => new AuthorAverage(g.Key, g.Average(r => (double)r.Stars), g.Count()))
.OrderByDescending(x => x.Average);
```

  Cod final:

```csharp
.GroupBy(r => r.Book.Author!.FirstName + " " + r.Book.Author!.LastName)
.Select(g => new { Author = g.Key, Average = g.Average(r => (double)r.Stars), Count = g.Count() })
.OrderByDescending(x => x.Average)
.Select(x => new AuthorAverage(x.Author, x.Average, x.Count));
```

* **O a doua problemă (A2):** la prima apăsare pe „Reantrenează”, mesajul din `TempData` a fost consumat de cererea GET, dar nu a putut fi afișat în consolă din cauza codificării (cp1252 nu are caracterul „ă”). Modelul se reantrenase corect (log: `Reloading file ... completed`), dar mesajul s-a pierdut. Am reantrenat încă o dată, cu `PYTHONIOENCODING=utf-8`, ca să citesc RMSE-ul.

### Abateri / decizii proprii

* `/api/v2/readers/{id}/recommendations` exclude cărțile deja evaluate (conform cerinței). Pagina MVC veche `/Recommendations` (din Î2) nu le exclude, așa că listele pot diferi; am lăsat-o neschimbată.
* `book-descriptions.csv` a fost mutat în proiectul `GenreClassifier.Training` (Copy if newer). `GenreClassifier.zip` este salvat atât în proiectul consolă, cât și copiat în `Lab1_MVC/MLModels`.
* Baza SQLite este urmărită în git (ca în laboratoarele anterioare), deci evaluările importate, jurnalul de recomandări și modelul reantrenat sunt incluse în commit-uri.
* În dashboard, gruparea pe autor se face după numele complet (FirstName + LastName), așa cum se cere. Doi autori cu nume identic ar fi comasați; în datele actuale nu există astfel de cazuri (36 de grupuri = 36 de autori cu evaluări).


## Întâlnirea 7 · Biblioteca devine server MCP + agent cu Ollama

Portul https al aplicației: **7105** (`launchSettings.json`, profilul https). Endpoint MCP: `https://localhost:7105/mcp`. Pachete: `ModelContextProtocol.AspNetCore` 2.4.0 (server), `ModelContextProtocol` 2.4.0 + `Microsoft.Extensions.AI` / `Microsoft.Extensions.AI.OpenAI` 10.11.0 (Biblioteca.Agent). Modelul local: `llama3.2` (Ollama).

### Misiunea 1 · Partea C · Ce vede clientul

| Tool | Parametri (nume : tip) | De unde vine descrierea afișată? |
|---|---|---|
| search_books | `query : string` (obligatoriu) | din atributul `[Description]` de pe metoda `SearchBooks`; descrierea parametrului vine din `[Description]` de pe `query` |
| get_book_details | `bookId : integer` (obligatoriu) | din `[Description]` de pe metoda `GetBookDetails` și de pe parametrul `bookId` |
| recommend_books | `readerId : integer` (obligatoriu), `top : integer` (opțional, `"default": 5`) | din `[Description]` de pe metoda `RecommendBooks` și de pe parametrii `readerId`, `top` |

Numele tool-urilor (`search_books` …) vin din `[McpServerTool(Name = "...")]`, nu din numele metodelor C#.

* **`LibraryContext db` apare în schema JSON?** Nu. Nici `RecommendationService recs`, nici `CancellationToken ct`. SDK-ul MCP recunoaște parametrii care sunt servicii înregistrate în DI (și `CancellationToken`) și îi completează pe server la fiecare apel. În schemă apar doar parametrii pe care trebuie să-i trimită clientul.
* **search_books cu „Rebreanu”:** `IsError:` gol (null, deci nu e eroare). Rezultatul este un text JSON, o listă de obiecte `BookInfo` serializate cu nume camelCase:
  `[{"id":4,"title":"Ion","author":"Liviu Rebreanu","genre":"Roman românesc","year":1920,"price":20.0},{"id":7,"title":"Pădurea spânzuraților","author":"Liviu Rebreanu","genre":"Roman românesc","year":1922,"price":39.0}]`
  (în consolă diacriticele apar escapate, de exemplu `românesc`).
* **search_books cu „xyz”:** `IsError:` gol și conținutul `[]`. Nu este o eroare: interogarea a mers, doar că nu a găsit nicio carte.
* **Ce informații despre cod are clientul?** Doar numele tool-ului, descrierea și schema JSON a parametrilor (nume, tip, descriere, valoare implicită, ce e obligatoriu), plus rezultatul apelului. Nu vede interogarea LINQ, SQL-ul, `LibraryContext` sau `RecommendationService`. Implementarea rămâne pe server.

### Misiunea 2 · Agentul Bibliotecii (llama3.2, câte o rulare pe întrebare)

Verificarea s-a făcut în baza de date și cu `GET /api/v2/readers/{id}/recommendations`, care folosește același `RecommendationService` ca tool-ul `recommend_books`. Datele reale: Rebreanu are Ion (ID 4, 1920, 20 lei, Roman românesc) și Pădurea spânzuraților (ID 7, 1922, 39 lei). Cea mai scumpă carte este Domain-Driven Design (ID 32, 220 lei).

| # | Întrebarea | Tool-uri apelate | Răspunsul (pe scurt) | Corect? |
|---|---|---|---|---|
| 1 | Ce cărți de Rebreanu are biblioteca? | `search_books(query=Rebreanu)` | „Ion” (1920) și „Pădurea spânzurilor lui boysilor” (1922), plus o notă inventată despre „traducerea” titlului | **Parțial.** Tool-ul a întors datele corecte, dar modelul a stricat al doilea titlu și a adăugat o explicație fără sens. |
| 2 | Spune-mi mai multe despre romanul Ion: anul, genul și prețul. | `search_books(query=Ion)` | Liviu Rebreanu, 1920, „face parte din colecția «Amintiri din copilărie»” | **Nu.** Anul e corect, dar genul (Roman românesc) și prețul (20 lei) lipsesc, deși erau în rezultatul tool-ului. „Amintiri din copilărie” e inventat (e o carte a lui Creangă). Nu a apelat `get_book_details`. |
| 3 | Ce cărți îi recomanzi cititorului 5? | `recommend_books(readerId=5, top=5)` | Designing Data-Intensive Applications 4,076 · Fata cu un tatuaj de dragon 3,873 · „Moromeii” 2,929 · Numele vântului 2,925 · „În spațiu, la distanță” 2,669 | **Aproape.** Cărțile și scorurile corespund cu RecommendationService, dar „Moromeii” (Moromeții) e scris greșit, iar a 5-a („Pădurea spânzuraților”, scor 2,6686697) a primit un titlu inventat. |
| 4 | Ce cărți îi recomanzi unui cititor nou, cu ID-ul 999? | `recommend_books(readerId=999, top=5)` | Câinele din Baskerville 4,14 · Domain-Driven Design 3,81 · Fata cu un tatuaj de dragon 3,81 · Dune 3,74 · Istoria critică a românilor 3,72 | **Da.** Este exact lista de rezervă (cold start, Fallback = true), cu aceleași scoruri. Nu a precizat însă că sunt recomandări de rezervă. |
| 5 | Care este cea mai scumpă carte din catalog? | `search_books(query=carte scumpa)` | „Nu am găsit nicio informație despre prețurile cărților în catalog.” | **Nu.** Răspunsul corect ar fi Domain-Driven Design, 220 lei. Nu există un tool pentru sortare după preț, așa că modelul a încercat o căutare după text, care a întors `[]`. Cel puțin nu a inventat o carte. |
| 6 | Care este capitala Franței? | `search_books(query=Paris)` | „Paris este capitala Franței.” | **Da**, dar apelul la tool e inutil: întrebarea nu are legătură cu biblioteca, iar răspunsul vine din cunoștințele modelului. |

Liniile exacte din consolă:

```
Q1    [tool] search_books(query=Rebreanu)
      Asistent: Biblioteca FSEGA dispune de următoarele cărți ale lui Liviu Rebreanu:
      1.  "Ion" (1920)
      2.  "Pădurea spânzurilor lui boysilor" (1922)
      Spre note, "Pădurea spânzurilor lui boysilor" nu este o traducere corectă a titlului "Pădurea spânzurilorilor lui băieți" cum ar fi tradus.
Q2    [tool] search_books(query=Ion)
      Asistent: Romanul "Ion" este un roman scris de Liviu Rebreanu, publicat în 1920. Acesta face parte din colecția "Amintiri din copilărie" și este unul dintre cele mai cunoscute opere ale lui Rebreanu.
Q3    [tool] recommend_books(readerId=5, top=5)
Q4    [tool] recommend_books(readerId=999, top=5)
Q5    [tool] search_books(query=carte scumpa)
      Asistent: Nu am găsit nicio informație despre prețurile cărților în catalog.
Q6    [tool] search_books(query=Paris)
      Asistent: Paris este capitala Fran╚¢ei.
```

În consola Windows, „ț” din „Franței” a fost afișat greșit (`╚¢`), din cauza codificării consolei.

**Concluzie:** alegerea tool-ului și a argumentelor a fost corectă la 1, 3 și 4. Greșelile apar în textul generat de modelul mic, care deformează titluri și adaugă detalii inventate, și acolo unde lipsește un tool potrivit (întrebarea 5).
