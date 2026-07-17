# Fluent API Design Proposal for Gedcom.Vector

This document proposes adding a Fluent API layer to `Gedcom.Vector`. The goal is to make it the easiest-to-use .NET library for both **building** family trees programmatically and **querying** complex relationships.

We propose two main fluent interfaces:
1. **Fluent Builder API** (`GedcomBuilder`): For constructing GEDCOM data structures programmatically.
2. **Fluent Query API** (`GedcomTreeContext`): For navigating and querying family tree relationships (spouses, children, parents, ancestral paths) with high performance ($O(1)$ lookups).

---

## 1. Fluent Builder API (`GedcomBuilder`)

Currently, creating a `GedcomParseResult` programmatically requires manually instantiating records and lists, which is verbose and error-prone:
```csharp
var persons = new List<PersonRecord> { new PersonRecord("I1", "John", "Doe", PersonSex.Male, ...) };
```

### Proposed Fluent Builder Syntax
```csharp
var result = new GedcomBuilder()
    .AddPerson("I1", "John", "Doe", PersonSex.Male)
        .WithBirth("1 JAN 1900", "New York, USA")
        .WithDeath("15 DEC 1980", "Boston, USA")
        .WithEvent(FamTreeEventType.Census, "1920", "Boston, USA")
    .AddPerson("I2", "Jane", "Smith", PersonSex.Female)
        .WithBirth("15 JUN 1905", "Chicago, USA")
    .AddFamily("F1", husbandXref: "I1", wifeXref: "I2")
        .WithMarriage("12 JUN 1925", "Chicago, USA")
        .WithChild("I3")
    .AddPerson("I3", "Bobby", "Doe", PersonSex.Male)
    .AddMedia("M1", "Family Portrait", "portrait.jpg", "jpg")
        .LinkTo("I1")
        .LinkTo("I2")
    .Build();
```

### How It Works Under the Hood
We implement helper builder classes that maintain state and return parent/child builders:
- `GedcomBuilder`: Orchestrates the root builder state.
- `PersonBuilder`: Focuses on adding attributes and events to the current person. Returns to `GedcomBuilder` on method chains.
- `FamilyBuilder`: Simplifies adding children and marriage details.
- `MediaBuilder`: Simplifies linking media records to individuals/families.

---

## 2. Fluent Query API (`GedcomTreeContext`)

Genealogical structures are flat in GEDCOM. Navigating relationships (e.g., "Find John's children" or "Find Jane's parents") requires writing complex LINQ queries over flat lists, which is slow ($O(N)$ scans) and highly repetitive.

### Proposed Fluent Query Syntax
We introduce a lightweight, index-cached `GedcomTreeContext` to enable rapid, fluent traversal of the tree:

```csharp
// 1. Initialize context from a parsed result (builds O(1) indexes under the hood)
var tree = result.ToContext();

// 2. Query relationships fluently
PersonRecord? john = tree.GetPerson("I1");
if (john is not null)
{
    // Get spouses of John
    IEnumerable<PersonRecord> spouses = tree.SpousesOf(john);
    
    // Get children of John
    IEnumerable<PersonRecord> children = tree.ChildrenOf(john);
    
    // Get parents of Bobby
    PersonRecord bobby = tree.GetPerson("I3")!;
    IEnumerable<PersonRecord> parents = tree.ParentsOf(bobby);
}
```

### Advanced Fluent Traversal (Chaining)
We can support chaining to traverse lines of descent:
```csharp
// Find all maternal cousins of a person
var cousins = tree.NavigateFrom("I3")
    .Parents()                        // Mother and Father
    .Where(p => p.Sex == PersonSex.Female) // Mother
    .Parents()                        // Grandparents
    .Children()                       // Mother and Aunts/Uncles
    .Where(p => p.XrefId != motherId) // Aunts and Uncles
    .Children()                       // Cousins
    .Execute();
```

---

## 3. Implementation Plan

### Phase 1: Fluent Builder
1. Create `GedcomBuilder` and sub-builders (`PersonBuilder`, `FamilyBuilder`, `MediaBuilder`) inside a new namespace `Gedcom.Vector.Builder`.
2. Build unit tests in `GedcomExportWriterTests` to verify round-trip exports of programmatically built trees.

### Phase 2: Fluent Query & Context
1. Create `GedcomTreeContext` containing optimized dictionaries mapping:
   - `personXref -> PersonRecord`
   - `personXref -> List<FamilyRecord>` (as spouse)
   - `personXref -> FamilyRecord` (as child)
   - `familyXref -> FamilyRecord`
   - `familyXref -> List<PersonRecord>` (children)
2. Expose extension method `result.ToContext()` to construct the context.
3. Write unit tests for relationship queries.
