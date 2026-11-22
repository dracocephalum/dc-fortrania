# Glossary

What the words used in this repository's code and documents mean here, one
line each. A word of FORTRAN's own keeps the standard's meaning; it is listed
where the code leans on it. Why something is as it is belongs in
[`DESIGN.md`](DESIGN.md), and what is planned in [`PENDING.md`](PENDING.md).

## The program being translated

| Word | Meaning |
|---|---|
| Program unit | One main program, subroutine, function or block data subprogram: what FORTRAN compiles on its own, and what the translator writes one file for. |
| Entry point | A way into a subroutine or function: its own heading, or an `ENTRY` statement inside it. Each becomes a method. |
| Dummy argument | A name in a procedure's heading that stands for what the caller passes. |
| Actual argument | What a call passes for a dummy argument. |
| Stray argument | A dummy argument of one entry point named by the statements of another, which the language forbids and programs do in branches never taken. |
| Statement function | A one-line function defined inside a program unit and known only there. |
| Kept variable | A variable that holds its value between calls: one declared `SAVE`, one initialized with `DATA`, or a local array too large for the stack. |
| Procedure shape | What a unit's own calls show of a procedure it was passed: its result and the types of its arguments. |
| Reference sources | The FORTRAN 77 sources of the SPICE toolkit that are translated. They are never committed. |
| SPICE Toolkit | NASA's library for the geometry of space missions, named for what its kernels hold: spacecraft, planets, instruments, orientation and events. "NASA's SPICE Toolkit" in prose; the legal origin, Caltech under a contract with NASA, only where a licence is stated. |
| NAIF | NASA's Navigation and Ancillary Information Facility, the group at JPL that maintains and publishes the toolkit: the subject wherever its rules, its server or its support are meant. Never "the owner". |
| Main library | The library of the reference sources that everything else of them is built on; its routines are what a user of the toolkit calls. |
| Reference compiler | The FORTRAN compiler whose behaviour the translation follows wherever the language leaves a choice. It settles questions by experiment. |

## The translation

| Word | Meaning |
|---|---|
| Translator | The library that reads FORTRAN and writes C#: source form, lexer, parser, analysis and emitter. |
| Emitter | The last stage of the translator, which writes C# source text. |
| Stack use | What a call of a procedure can take from the stack: its own method's `stackalloc` bytes and those of the deepest chain of calls beneath it, the just-in-time compiler's frames left out. |
| Working buffers | What a method needs only while it runs, its local arrays, character variables and places where values are joined, as against the saved state a unit keeps between calls. |
| Block mode | A context's choice to keep working buffers at fixed places in a block it holds, laid out at translation, rather than on the stack, the stack mode. |
| Region | The part of a context's block that the procedures of one translated project keep their working buffers in, each at its place. |
| Level | One block of a context's working memory. A procedure called through an argument runs at the next level, so that it meets no buffer of the chain that led to it. |
| Program tree | A program unit once the emitter has decided how everything in it is done, with nothing of FORTRAN's left in it: a file of members, methods of statements, statements of expressions. It lies between the analysis and a writer. |
| Text block | The character constants of one translated library, each once, as bytes its class keeps in the assembly. A method reaches a constant by where it starts in the block. |
| Pass | A step that takes a program tree and gives one, an optimization or a lowering for a writer. |
| Writer | What walks a program tree and writes one language's source, choosing only spelling and layout. |
| Summary | What one program unit shows to the others: how it is called, and what it calls. Units are checked against each other by summaries, never by syntax trees. |
| Convention | The shape translated procedures present to their callers: how names are spelled, how a logical result comes back, where the length of a character argument goes. |
| Runtime | The small hand-written library translated code calls for what FORTRAN has built in: character values, input and output, integer powers. |
| Translated layer | The emitted classes as they are, every argument a pointer. Fast, faithful and not meant to be pleasant. |
| Managed layer | The hand-written API over the translated layer, with .NET types, exceptions and safety. |
| Source | In a translation's settings, one FORTRAN library: its files and the name of the class they become. |
| Project | In a translation's settings, one assembly, made of one or more sources. |
| Forwarder | A static method that finds a library in a context and calls one of its routines; its address is what stands for the routine where a procedure is passed as an argument. |
| Variance | A place where translated code or the runtime knowingly behaves otherwise than the reference; each is listed in [`VARIANCES.md`](VARIANCES.md). |

## The program as it runs

| Word | Meaning |
|---|---|
| Context | One running FORTRAN program: its units and files, its screen and keyboard, the memory its libraries keep variables in, and one instance of each of its libraries, made when first asked for. It serves one caller at a time. Two contexts share nothing. |
| Handle | A number that stands for a context where an object cannot be passed, in code that is not .NET above all. |
| Library | A translated class, and an instance of it: one FORTRAN library as one context has it. It computes, and is handed everything else by its context. |
| State block | The run of bytes in a context's memory where one program unit's kept variables lie. |
| Unit | A number by which a FORTRAN program refers to an open file. Units belong to the context, so every library of a program sees the same file under the same number. |
| Standard units | Units 6, 0 and 5: the screen, the error output and the keyboard, until a file is opened on them. |
| Record | What one input or output statement transfers: a line of text, or a run of bytes of fixed length. |
| Direct access | Reading and writing records by their number, in any order. |
| Sequential access | Reading and writing records one after another. |
| Formatted | Transfer as text, by a format or list-directed. Unformatted transfer copies a value's bytes as they are. |
| Format | A character value that says how the items of a statement are laid out in records. |
| List-directed | Formatted transfer with no format, where each item's type says how it is written or read. |
| File provider | What decides what a file name stands for: a path on disk, a stream or bytes registered under the name, or anything else that holds bytes. |
| Test family | One routine of the toolkit's test program, named `F_` and what it tests, holding the test cases of one routine or a few. The program runs 400 of them. |
| Status | The number an input or output statement gives: zero for success, negative at the end of a file, positive for a failure. `IOSTAT` receives it. |
