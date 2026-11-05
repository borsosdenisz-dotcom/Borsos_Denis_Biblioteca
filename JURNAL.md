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

(se completează la S2)

## S3 · Filtrul variantei mele (V3)

(se completează la S3)

## S4 · Folosirea AI

(se completează la S4)
