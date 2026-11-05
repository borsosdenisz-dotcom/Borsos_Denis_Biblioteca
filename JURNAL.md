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

(se completează la S4)
