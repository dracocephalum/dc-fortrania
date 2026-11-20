# Design

Why the repository is shaped the way it is. Type comments say what the code
does; this page holds the decisions a reader could not infer from it.

## Translated code is committed, and it is an intermediate product

The library is produced in three steps:

    FORTRAN 77 sources  --translator-->  C# sources  --compiler-->  NuGet package
    (never in this repository)           (committed)                (what is published)

The middle step is committed on purpose. A change to the translator then
shows up in review as a diff in exactly the routines it affects, a build
needs neither the original sources nor a translator run, and anyone can read
what the published assembly was compiled from.

It is still machine output, and it is treated as such:

- It is never edited by hand. A wrong translation is fixed in the translator
  and the output regenerated.
- It is not the product. The package compiled from it is, together with the
  hand-written API over it.
- It lives in components of its own, apart from hand-written code, so that
  its licence and its relaxed build settings cover nothing else.

The rule that follows from this is
[`docs/rules/translated-code.md`](docs/rules/translated-code.md).

## Two licences, split by component

Hand-written work is under the Apache License 2.0: the translator, the
runtime library, the public API, tests and documents. Translated code is not.
It derives from NASA's SPICE Toolkit, which has no open-source licence and
is governed by the rules NAIF publishes for it, so it stays under those
rules.

The split is by folder and never by file within a project. A component that
holds translated code has its own `LICENSE.md` and holds nothing a person
wrote; every other component is covered by the root [`LICENSE`](LICENSE) and
[`NOTICE`](NOTICE). A reader can therefore tell which terms apply from where
a file is, and each translated file repeats it in its header for the reader
who arrives at the file alone.

## Byte order is the format's concern, not ours

Binary kernels record their own byte order and the reference toolkit converts
when it reads one of the other order. That logic is translated with the rest,
and the .NET runtime runs on both orders, so the library relies on the two of
them rather than adding a layer of its own. What hand-written code owes is
narrower: it never assumes an order —
[`docs/rules/portability.md`](docs/rules/portability.md).

## Fortrania's lexer finds no keywords

FORTRAN 77 gives blanks no meaning outside a character constant and reserves
no words. `DO 10 I = 1, 5` and `DO10I=1,5` are the same statement, and
`DO10I = 1.5` is an assignment to a variable. Whether a statement begins with
a keyword can only be decided by looking at the whole of it.

So the work is split. `Lexer` removes blanks and returns a run of letters and
digits as one name, without looking for keywords. The parser first decides
what kind of statement it has, and then splits a keyword off the front of a
name where that kind calls for one.

The alternative rejected was to treat blanks as separators, as later
standards do. It would lex the reference sources, which are written with
blanks in the conventional places, but it would not be FORTRAN 77, and the
translator is meant to be general.

## What a name means is kept per program unit, not per use

In FORTRAN 77 a name stands for one thing throughout a program unit: a
variable, a named constant, a function, a subroutine. `F(X)` is an array
element everywhere in the unit or a function reference everywhere in it,
and the text of one statement cannot say which. Declarations and every use
in the unit decide it together.

So the parser leaves `F(X)` undecided, as one kind of node, and analysis
answers once per name: the `SymbolTable` of a unit gives each name its kind
and type. A later stage that meets `F(X)` looks `F` up. No second tree with
the answer written into each node is built, because the answer never
differs from node to node.

The two exceptions are narrow and are handled where they arise. Inside a
function its own name is the variable that holds the result. And a statement
function's dummy arguments are names of that one statement, sharing only
their type with the unit's variable of the same name.

## What crosses program units is a summary, not a tree

To translate a call, Fortrania has to know the procedure called: how many
arguments it takes, which are arrays, what type each is. That is a fact about
another program unit, often in another file.

Each unit is therefore reduced to a `UnitSummary`: how each of its entry
points is called, and every call it makes itself. A summary holds nothing of
the unit's text. A program is read in two passes: the first keeps only the
summaries, in a `ProcedureCatalogue`, and the second reads each file again
and translates it with the catalogue at hand. No more than one file's tree
is in memory at a time, and a file needs translating again only when it, an
include file it names, or the summary of something it calls has changed.

The role is that of a C header, except that nobody writes it: FORTRAN 77 has
no declarations of other units' procedures, so the summary is drawn from the
definition.

Calls are set against definitions with a light hand. The language asks that
arguments agree in type and shape, but a compiler that takes one file at a
time cannot check it, and programs written for such compilers lean on
arguments being passed by address: one value where an array is expected, an
array of one type read as another, a function called as a subroutine for its
side effects. `CallChecker` passes all of these and refuses only what no
address can stand in for, such as a wrong number of arguments or text where
a number is expected.

Whether a procedure reads or writes an argument is often decided several
calls down, where the argument finally meets an assignment. A summary
therefore records two things per dummy argument: what the unit's own
statements do with it, and each place it is passed on. `ArgumentEffects`
follows those places through the summaries of the whole program until the
answers stop growing. It never needs a tree, and it can be given the answers
of a library worked out earlier.

The answer is kept per entry point, not per program unit. A unit with
several `ENTRY` statements is commonly one routine that stores a value and
another that returns it, sharing a dummy argument's name; taken as a whole
that argument would be both read and written, and so would every argument
passed to either routine anywhere in the program. Each entry point answers
only for the statements control reaches from it.

Where nothing can be known the answer is both: an argument passed to a
procedure that is itself a dummy argument, or to one the program does not
define. And nothing is said about order, so an argument that is written and
then read back counts as read.

## What emitted code looks like, and why

The emitter writes each program unit as methods of one partial class, every
argument a pointer; `PENDING.md` has the reasons for pointers and for the
class, which are settled but not all built. What is built so far made these
choices of its own.

**A value with no address is passed through an expression.** A constant or
an expression given as an argument has to be stored somewhere its address can
be taken. A statement before the call would do that, but a call can stand in
the condition of an `ELSE IF` or a `DO WHILE`, where there is no place for a
statement that runs each time the condition does. So the store is itself an
expression: `Arg(&_i1, n + 1)` stores into a local set aside for it
and returns its address, and the just-in-time compiler inlines it to the
store. The locals are reused from statement to statement.

**A logical value is an integer in storage and a `bool` in an expression.**
The language gives a logical variable the room of an integer and lets the
two share storage, and programs pass arrays of one to routines that move the
other. Stored as `bool` they would not be the same size. In an expression
the natural C# type is kept, so a condition reads as written.

**An integer power multiplies, in the reference compiler's order.** `X**N`
with an integer `N` is repeated multiplication, and the order of the
multiplications decides the last bit. The runtime's `Pow` multiplies in the
order the reference compiler's library does, so the results agree exactly.
A real exponent goes to the platform's `Math.Pow`, which may differ from
another platform's in the last place.

**A `DO` loop takes one of two forms.** The language fixes the number of
passes before the first and leaves the variable one step past the last value
used. With an integer variable and a step known when translating, a plain
`for` with a comparison against the limit does exactly that and is what a
person would write. Otherwise the count is worked out first, as the language
defines it, and counted down.

**FORTRAN names are written in lower case, and the methods keep their
spelling.** A variable `STATE` becomes `state` and the subroutine `VADD`
stays `VADD`, so no variable can meet a member of the class, the two differing
in case, and the code reads beside its source. A name that is a C# keyword
once in lower case takes the `@` escape. Locals the emitter adds begin with
an underscore, which begins no FORTRAN name.

**The emitter is tested by running what it writes.** The tests translate
FORTRAN, compile the result with the C# compiler as a library, and call it.
The values they expect are the ones the reference compiler's program prints
for the same source, so the tests say what the code does and not how it
reads. One test compares text, to keep the shape above from drifting
unnoticed.

**A character value is an address and a length.** There is no string
object and no terminator: a variable is so many bytes, an array of them is
those end to end, a substring is an address further in with a shorter
length, and a constant is the address of a `u8` literal, which the compiler
keeps in the assembly where nothing moves it. So taking a substring or an
element costs an addition and passing one costs two arguments, and nothing
is copied or allocated until an assignment copies. Assignment and comparison
are calls to the runtime, which pads the shorter with blanks as the language
defines. A dummy argument takes its length from the caller even when its
declaration fixes it, because the caller cannot know which it is.

**A character value that is made needs a place, and gets the cheapest that
is right.** Concatenation and a character function's result are values
that were not there before. Joined in an assignment, the pieces are copied
straight into the variable assigned to, one after another, and nothing is
copied twice. That would go wrong where a piece is part of that same
variable, since what is written first would be read afterwards; the emitter
sees that by the variable's name, and then joins elsewhere first. Anywhere
else, as an argument or in a comparison, the value lies in a local of the
method set aside for it. Such a local is as long as the value can be at
most, which is known when translating for everything but a dummy argument
of assumed length, and the language does not allow one of those in a
concatenation outside an assignment. The locals are declared once at the
top of the method and used again from statement to statement, so a loop
does not grow the stack. A character function is handed the place for its
result, of the length the calling unit declares for it, and gives the
address back, which makes a reference to it a value like any other.

**Constants are joined when translating.** The values of literals and
named constants are known then, so a concatenation of them alone is
written as one literal, however it is grouped, and needs no place at all;
constants side by side among other values are one piece. Most of the
toolkit's concatenations are of this kind, an error message or a test's
title written across continuation lines: of 18,062 joined when run, 442
are left. The bytes are the same, so there is no setting for it. That is
the rule for what the translator writes more simply than the source says:
what keeps the behaviour exactly is always done, and only a different
behaviour would be a choice, among the variances.

**What a unit keeps between calls lies in a block of the instance, and is a
pointer inside the method.** Kept are the variables a unit declares `SAVE`
or initializes with `DATA`, the second because every compiler since has
kept those and programs count on it, and local arrays too large for the
stack, which may be kept since the language has no recursion. Each unit has
one block of the program's memory for them, handed out zeroed on the
unit's first call, when the `DATA` values are stored; a value of zero needs
no storing, and a run of equal values is one fill. A method that names a
kept variable loads the block's address, tests it, and declares a pointer
into it for each one it names. From there on a kept variable is written exactly as a
dummy argument is, which is also a pointer, so the rest of the emitter does
not tell the two apart. The memory is the program's, held by its context,
and is freed when the context is disposed; the garbage collector does not
free it.

**A translation says how much stack each procedure can take.** The emitter
counts what each method takes with `stackalloc` and which procedures it
calls, passes and calls through an argument; the translator follows the
calls, which form a graph without cycles since the language has no
recursion, and gives each procedure its deepest chain, in
`TranslationResult.StackUse`. A procedure passed as an argument is counted
where the procedure it reaches calls one it was passed, beneath the call
that passed it, and the figure is an upper bound because which argument is
called there is not told apart. Counting a passed procedure on top of the
caller's deepest call instead, the simpler bound, compounds through the
toolkit's searches, which pass their functions down several levels, and
came to 587 KB where this comes to 76 KB. The figures leave out the
just-in-time compiler's own frames, which come on top. The same result
lists each variable `EQUIVALENCE` puts off its type's alignment, for
`docs/rules/portability.md`, *Alignment*. The SPICE tool writes both beside
the translation; `PENDING.md`, *How much goes on the stack is a setting*,
has what they showed.

**A translated library computes, and is handed everything else.** What the
libraries a FORTRAN program is linked from share, they share because they
are one process: one set of units, one screen and keyboard. Here that is a
`FortranContext`, and a translated class is made over one: its constructor
takes the context, keeps the context's memory and its input and output,
and adds itself to the context. Beyond what its routines keep between
calls, an instance owns nothing and decides nothing; whoever makes the
context chooses where files come from and where the screen goes, and
disposes it. Two libraries made over one context are two parts of one
program, and see the same unit under the same number. Two contexts are two
programs that know nothing of each other, on as many threads. The class's
one file that belongs to no unit holds that constructor.

**A call into another library is a call on that library's instance.** The
emitter is told which other libraries the one it writes may call, and a
name the library does not define itself is looked for in them. Such a call
is written on a member of the class that gives the other library, and that
member asks the context for it the first time it is used and keeps it. It
is asked then and not when the class is made, so the libraries of a program
need no order, and two may call each other, as two libraries linked into
one FORTRAN program may.

**A context makes a library the first time it is asked for it.** Every
emitted class says how it is made over a context, through a static member
its interface requires, so `Get` needs no reflection and nothing registered
beforehand: it is to the context what resolving a singleton is to a
container. A program is then one object to its host, the context, and
whatever it calls pulls in what it needs. A library made by hand first is
the one `Get` gives, which leaves room for a class derived from a library's.

**A context can be named by a number, and none is there by default.**
Code that is not .NET cannot hold an object, so a context gives a handle
that stands for it and is turned back into it: a place in a table and the
turn that place is on, so that the handle of a context that is gone is
refused and not taken for its successor's. The runtime has no context of
its own beside those: none is made unasked and none is the process's by
default, since new code has no reason to prefer a call that names no
context, and a default that made itself would hand a library the console
and leave to run commands that nobody granted. The one thing that needs a
default, a library that stands in for the reference toolkit under its own
function names, keeps one for itself.

**What a host decides for a program is one object.** `FortranContextOptions`
holds it: where files come from, whether the console is the program's, its
command line, whether it may run commands, its clock, what `RECL` counts
and how a line ends. Left as made it gives nothing but files on disk, and
`Standalone` is what a FORTRAN program run on its own has. A context is
made from one. A call inside a library is unchanged,
and one across libraries costs a load of that member more.

**Variables that share storage are pointers into one run of bytes.**
`EQUIVALENCE` says that two variables, or elements of them, begin at the
same byte. The emitter gathers the variables that share, directly or
through one another, works out where each begins in bytes from the start of
the set, and declares each as a pointer to its place, of its own type. The
set lies in the unit's block when any of its variables is kept between
calls or it is too large for the stack, and on the stack otherwise. Nothing
converts: a number read through a variable of another type shows the other
type's bytes, in the order the machine keeps them, which is what the
language says and what programs use it for. A variable may begin where its
own type would not be aligned; x64 and ARM64 read it all the same, and
32-bit ARM may not, as `docs/rules/portability.md` has it under
*Alignment*. Two lists that put one variable in two places are an error in the
source, found here because this is where places are worked out.

**An entry point is a method of its own.** A unit with `ENTRY` statements
has several ways in, and is commonly one routine holding saved variables
with an operation per entry. Each entry point, the unit's own heading
included, becomes a method that takes that entry's dummy arguments and
holds the statements control passes through from there, as `EntryLayout`
gives them. Kept variables are shared through the unit's block. Other
locals belong to each method, since the language does not keep them between
calls. In a function the results of all entry points are one variable under
several names, so each of those names is the result of the method it is
written in; results of different types, one stored through the name of
another, are refused. Where control runs on from one entry point into the
next, the first method holds the statements of both: the same statement is
then written twice, and no call stands in for running on, because a call
would give the second its own locals. The form `f2c` writes, one body with
every entry's arguments and a selector, is always correct too, and was not
chosen because every call then passes every entry's arguments.

**A method declares only what its statements name.** A method for one entry
point names few of its unit's variables, and a local array it does not name
would still be taken from the stack on every call; a method that names no
kept variable does not ask for the unit's block. The same holds for a unit
with one way in. The language forbids an entry's statements to name a dummy
argument of another entry, since nothing was passed for it, and programs do
it all the same in branches never taken. The method declares such a name as
a local holding zero or blanks, an array with one element and text of
assumed length one character, so that the statement does something
harmless where the original would follow an address nobody set.

**A statement function is a local function that captures nothing.** It is
written in each method that refers to it. Its own arguments are passed by
value, a character one as the address of its first byte, since an
expression can only read them. Where its expression names a variable of
the unit, that variable is passed as well, under its own name and in the
form the method has it, a value or an address, so that the expression
reads the same inside the function as outside. C# would let the function
use the method's variable directly, and would then keep that variable in
memory for the whole method and not in a register, which is the wrong
trade for a function called from a loop. A function that calls no procedure
of the program is declared `static`, and the compiler then refuses a
capture written by mistake. Not translated yet are a statement function of
type `CHARACTER`, an argument of assumed length, and a logical argument
passed on to a procedure.

**A procedure is passed as a function pointer that takes the program's
context first.** C# gives no address of an instance method, so the unit
that passes a procedure of the program has a static forwarder to it in its
own file, which takes the context and then the procedure's arguments, finds
the procedure's library in the context, and calls it; the forwarder's
address is what is passed. Calling through the pointer is an indirect call
with the context in front. Nothing is allocated, as a delegate over an
instance method would be on every pass. It is the context in front and not
the library, because the procedure passed may belong to another library
than the one that calls it, a routine of the program's own handed to a
routine of the library it uses, and the context is what both have. Finding
a library in a context is one load from a list, at a place fixed for the
library's class. The same argument lets a layer above bridge to anything
else: it keeps a native pointer or a delegate where the forwarder it passes
can find it.

**The pointer is typed where the unit calls it, and `void*` where it does
not.** The language checks nothing about a procedure argument, and a unit
that only passes one on says nothing of what it takes, not even whether it
is a subroutine. So a unit's summary records, for each dummy procedure, the
shape its own calls show, `ProcedureShape`, and the parameter has that type;
a caller in C# then gets a compile error for a method of the wrong shape.
Where the unit never calls it, or two of its calls take it differently, the
parameter is `void*` and is cast at each call. A pointer is likewise cast
wherever it goes from one of these forms to another. The casts exist for
the compiler alone: nothing is checked when the program runs, and a
procedure of the wrong shape does there what it does in FORTRAN. Not
translated yet are a function of the language passed as an argument, and a
procedure passed that the program does not define.

**What a procedure looks like from outside is a convention, and can be
replaced.** Its name, the type of a logical result and the place of a
character argument's length are asked of an `EmitConvention`, not decided
in the emitter. Fortrania's own keeps the FORTRAN name, returns a `bool`
and puts each length directly after its address, where the two read as the
pair they are; the one for `f2c` gives the lower-case name with its
underscore, returns an integer and puts all lengths after all other
arguments, so that code written against that translator's output finds what
it expects. The translation
inside a procedure does not change with the convention.

**Every emitted file switches off the diagnostics it is known to raise.** A
faithful translation has statements that cannot be reached, variables set
and never read, and variables assigned to themselves, because the source
has them. The emitter names those three in a `#pragma` at the top of each file and adds whatever the options
list, so that a project with warnings as errors can take the files as they
are. A convention with lower-case method names is the usual reason to list
more.

**The runtime's methods are called with no name before them.** Every
emitted file has `using static` for the runtime's class, so what the
translation adds reads as `Assign(key, 8, name, name_len)` and stays small
beside what was translated. The options can ask for a short name instead,
`F.Assign(...)`. Inheriting the methods from a base class would read the
same, but when the runtime is compiled in as source its types are internal,
and a public class cannot derive from an internal one; nor would the
emitted class then be free to take a base of the user's. Extension methods
are not open to it either, since a pointer cannot be the value one extends.
Inside the emitted class a method of the class is found before anything a
`using` brings in, so where the program has a procedure emitted under the
very name that would be written, the runtime's method is written with the
full name of its class.

**An input or output statement is calls on the program's `FortranIO`.**
The translated code knows a file as FORTRAN does, by a name when it opens
it and by a unit number afterwards; what is behind a unit is the runtime's
alone. `OPEN`, `CLOSE`, `INQUIRE` and `REWIND` are one call each, with the
specifiers the statement gives passed by name and the rest left out. A
`READ` or `WRITE` is a call that begins it, one for each item of its list,
and one that ends it, so that an implied loop in the list is an ordinary
loop around the item's call. Each statement gives a status. It is stored
where `IOSTAT` says, and where the statement has none it goes to `Require`,
which throws for anything but zero: FORTRAN ends the program there, and a
library must not.

**The status values are the reference compiler's.** The sources test only
whether a status is zero, negative or positive, but they print the number
in their messages, so the runtime gives the number the reference compiler
gives for the same failure, each one measured: the operating system's for a
file that is missing or already there, and the compiler library's own from
5001 on.

**Where the runtime behaves otherwise than the reference compiler, it is on
purpose and it is written down.** [`VARIANCES.md`](VARIANCES.md) lists every
such place, with what the reference compiler does, what the runtime does
and why; a new one is added there in the change that makes it.

**A record read by number is one read from the file.** Direct access
divides a file into records of the length `OPEN` gives, and a statement
names its record. Reading fetches the record once, into a buffer the
instance keeps, and copies each item out; an item that is the whole record
is read straight to where it goes. Writing puts the items together in the
buffer, zeros where they do not fill it, and writes the record when the
statement ends, so a statement that fails writes nothing. Bytes are copied
as they are, in the machine's order. What the unit of `RECL` is the
language leaves to the compiler: bytes here by default, as in the reference
compiler, and a property of `FortranIO` for the compilers that count words.

**A file name goes to a provider, and a stream can be given a name.** By
default a name is a path on disk, opened through the operating system's own
handle and read and written a record at a time with no buffer in between; a
file that may only be read is opened for reading. A seekable stream
registered under a name is that file from then on to every statement, and
is left open when its unit is closed. A stream has one position, so it
serves one instance. Bytes held in memory can be registered too, as a file
to read and not to write: every read says where it reads, nothing is kept
between reads, and so one copy of a kernel in memory can be registered with
any number of instances and read by all of them at once. The provider can
be replaced altogether. Two names are one file when the provider says so, which for the
disk is the full path, compared without regard to case where the file
system has none.

**A format is followed as the items come.** A format is a character value,
most often a constant and sometimes put together when the program runs, so
the runtime reads it then, and keeps the last one read since a statement is
mostly run again with the format it had. A formatted `READ` or `WRITE` is a
call that begins it with the unit and the format, a call for each item, and
one that ends it. Each item's call follows the format to the next part that
takes a value, doing on the way what the parts before it ask, blanks, text,
the end of a record, and edits the item as that part says; when the format
is used up and items are left it starts again as the language lays down,
on a new record. The item's call is typed as the item is, an integer, a
real, a logical or a character value, which is how the runtime can refuse
an integer under `F` editing as the reference compiler does. What the
runtime writes for each kind of editing was set against the reference
compiler's output line by line, and the tests keep that output.

**A number is rounded from its exact value, half way to even.** The digits
come from the framework's own conversion, which is exact to any length, and
are then laid out as the format asks. The framework rounds a number that is
exactly half way upwards, and the reference compiler's library to the even
digit, so that case is settled here: where twenty digits past the last one
kept still look like a half, the number's whole expansion is taken and
looked at. It is rare, and only it pays for the long conversion.

**Text in sequence is lines, and bytes.** A unit opened for formatted
sequential access reads and writes records that are lines. Reading finds
the end of a line in the bytes itself, in a part of the file read ahead for
the unit, and takes a line feed, a carriage return, or the two together for
it, so a file made on one system is read on any other. Writing ends a
record with the running platform's line ending, which is what the reference
compiler writes there, or with whatever `NewLine` is set to. A record
written becomes the last of its file, as the language has it: what followed
it is cut off when the unit is next rewound or closed. Nothing is decoded
or encoded on the way, so text that is not ASCII passes through as the
bytes it is.

**A character variable read or written as text is an internal file.** The
statement gives the variable's address, the length of a record, and the
number of records: one for a variable, an element or a substring, and one
for each element of a whole array. Everything else is formatted or
list-directed transfer as it is for a file, so a format behaves the same
whatever its records are written to. A record is written to the variable
when it is whole, blanks after what was written, and one that does not fit
is not written at all.

**The screen and the keyboard are units 6, 0 and 5 until a file is opened
on them.** An asterisk for the unit is 6 to write and 5 to read. They go to
whatever writers and reader the host sets, and nowhere until it sets them:
what is written is dropped and a read finds the end of the file. An
instance is one of perhaps many in a program, on as many threads, and the
console belongs to the program and not to any of them; `UseConsole` hands
it to the one instance that is to have it. Here alone text is decoded,
since a writer takes characters and has an encoding of its own.

**List-directed output is laid out as the reference compiler lays it out.**
The language leaves the layout to the compiler, and a program written
against one compiler reads what that compiler writes, so the runtime
writes the same: a blank to begin the record, an integer in eleven columns,
a number to all the digits its type holds, plain where it is neither very
large nor very small and with an exponent otherwise, a character value as
it is, and a blank between two items unless both are character values. The
record is one line however long. Reading takes values apart at blanks,
commas and the ends of records, with what the language allows there: a
value in apostrophes or quotation marks, which may run on into the next
record, a count and an asterisk before a value given several times, two
commas for an item that keeps its value, and a slash that ends the list.
The item's own type says how its value is read, as with a format, and the
item's call is the same call.

**A main program is a method that takes nothing.** It is emitted like a
subroutine, under the program's name, or `MAIN` where the source gives it
none. What starts it is not the translated code's business: a host calls
the method, and a program translated to run on its own is to get a `Main`
that does.

**What a program takes from its compiler's library, the context supplies.**
Six procedures that no standard of the language defines are called by the
reference sources and by most programs of their time: `EXIT`, `IARGC`,
`GETARG`, `GETENV`, `DATE_AND_TIME` and `SYSTEM`. A call of one is written
on the program's context, which has a method for each. They are the
context's because they are about the program and not the library: its
command line, its clock, which the host can replace, and whether it may
have the operating system run a command, which it may not unless the host
says so. `EXIT` throws as `STOP` does. A procedure of the program that has
one of these names is the program's own and is called as any other.

## What is translated is set out as projects and their sources

One settings file names everything a translation produces, and
`Translator.Run` does the whole of it. The file lies in the project that
hosts it, and the paths in it are taken from where it lies. It is JSON
that may hold `//` and `/* */` comments and trailing commas, as .NET's own
`appsettings.json` may, because a file a person edits needs the reason
beside the setting; standard JSON has neither, and a strict reader
elsewhere refuses such a file.

    {
      "output": "src",
      "projects": [
        {
          "name": "Dracocephalum.Spice.Toolkit",
          "sources": [
            { "name": "SpiceLib", "files": [ "spicelib/*.f" ] },
            { "name": "Support",  "files": [ "support/*.f" ] }
          ]
        },
        {
          "name": "Dracocephalum.Spice.TSpice",
          "entry": "MAIN",
          "sources": [
            { "name": "TestUtil", "files": [ "testutil/*.f" ] },
            { "name": "TSpice", "class": "TSpiceMain", "files": [ "tspice/*.f", "tspice/*.pgm" ] }
          ]
        }
      ]
    }

- **A project is an assembly, and each of its sources a class.** A source
  is one FORTRAN library: its files, always given as a list, and a name
  that is its class. Two libraries in one assembly stay two classes, so
  that a library's class is the same however it is packaged and two
  libraries may each have a routine of one name. The namespace is the
  project's name unless it is given.
- **A class may not have the name of a part of its namespace**, which C#
  allows and every use of the name then trips over. The translator refuses
  it and asks for a class name; it does not rename by itself, since the
  class would then not have the name that was written.
- **What a project is follows from what is in it.** With a main program
  among its sources it is an executable, and a library otherwise; either
  can be said outright. An executable has one entry: with several main
  programs the project names the one, and until it does the translation is
  refused.
- **A project file is written once.** Where the project has none, the
  translator writes one, with a `Main` for an executable, and that is a
  whole project from nothing. Where it has one, the translator writes
  sources and nothing else, so a project made or changed by hand stays as
  it is; the checks on an executable's entry are not made then, since no
  `Main` is written. Translated sources go to a folder of their own in the
  project, a folder per source, and a new translation removes there only
  files that say they were generated. The classes are partial, so
  hand-written files beside them add to the same classes.
- **References between projects follow from the calls**, since the
  translator knows which source defines every routine called. Left out,
  they are worked out, and written into a project file being made or
  reported as missing from one that exists. Given, they are used and
  checked. Projects that need each other in a circle are refused; sources
  of one project may call each other freely.
- **All of it or none.** The settings are checked, every source read and
  checked, and every unit emitted before the first file is written, so an
  error anywhere leaves what is on disk as it was. A file whose text is
  already what it should be is not written again, and a build that looks
  at dates sees no change.

**Commands run after a translation, and change what they like.** What a
translation should do beyond translating, mending one routine for the
platform it runs on above all, is not the translator's to know. So the
settings list commands, `onSuccess`, that are run once the whole
translation is written: each a program and its arguments, no shell
between, in the settings file's folder, and told in its environment where
the settings and the output are and which files were written. Such a
command can bring whatever it needs, a C# syntax tree and its rewriters
among them, and the translator depends on none of it. One that fails fails
the translation, with what it said; the files are written by then, so the
translation's own all or nothing does not reach the commands after it.

**A path may name a variable, so that a shared settings file names no
machine.** Wherever the settings give a path, `$(Name)` stands for a value
looked for first in a local settings file beside them, of the same name
with `.local` before the extension and meant to stay out of version
control, then in the environment, then under `variables` in the settings.
Where a translation's sources are on one machine is said there, and never
in the file that is shared. A variable with no value anywhere is reported
before anything is read.

**A variable can say where to download the folder it names.** Besides a
value, a variable may give an archive's address, its SHA-256 hash and how
many leading folders to drop. When the folder is not there, it is filled
before anything is read; when it is, nothing is fetched, so a developer's
own link to the sources wins and a fresh checkout on a build server fetches
them. The hash is required, and an archive whose hash differs is refused,
so what is translated is always what was named. The archive is written and
expanded beside the folder and moved into place at the end, so an
interrupted fetch leaves no folder that looks whole; an entry whose path
would leave the folder is refused, and links in it are passed over. A value
given by the local settings file or the environment is somebody's own
folder and is used as it is. The formats are those .NET reads by itself:
zip, tar, and tar compressed with gzip.

A run can be forced, and then writes every translated file again and lists
it as written, whatever the manifest says, for a rebuild that is to start
from nothing.

**A translated file says what it was translated from, and no more.** Its
header names the program unit and the file it came from, without a folder,
so that nothing of the machine it was translated on is in it, and no date,
so that the same sources give the same bytes. The translator's version is
kept in the manifest and not in every file, so that a new translator
changes only the files whose translation it changes. What a particular
library has to say in its files, a licence notice say, is added by a
command after the translation.

**The source's comments are carried, where they stand.** A comment says
what the code beside it is for, and a routine of the reference sources
opens with its documentation, the disclaimer the toolkit's rules require
among it; a translation without them is harder to read and is not
allowed to be published. Each comment line becomes a `// ` line at the
indentation of the code it comes before, with the text after the comment
character less the indentation the lines of its block share: in fixed
form that indentation stood for the code's, which the C# has its own of,
and what is lined up within the block stays lined up. Line comments with
a space are C#'s own, StyleCop's SA1005; `f2c` writes `/* */` since C had
no other, and a block comment would need its text kept from holding `*/`.
A blank line stays one, and a run of them is one; a block of comments that
follows code has one before it, as SA1515 asks, and one that comes before
a closing brace has none after it. Where the code does not stand where
the source has it, the comments go where it does: those before the statement that names a
procedure go before its method, those up to the first executable
statement go at the top of the method before what it declares, since
declarations are gathered there; those before an `ELSE` go inside the arm
it opens, and those after the last `END` of a file follow the last
method. An entry point's method has the comments that follow its `ENTRY`.
Comments of an include file are the include file's, and are not carried
into each unit that includes it. Of the toolkit and its tests, 811,320
comment lines, each is carried once; the main and support libraries'
translation grows from 12 to 37 MB.

**What the translator wrote is recorded, so that what a command changed is
kept.** Each folder of translated sources has a manifest with a hash of
the text the translator gave each file. A file is written again when that
text changes or the file is gone, and not because it differs from what is
on disk; a file a command changed stays changed until the FORTRAN behind
it does, and is then listed among the files written for the command to
change again. A command that changes a file keeps its first line, which is
how a file the translator no longer makes is told from one somebody wrote.

The settings file, the command line and the build step all read into the
same description, kept in the translator's library and not in any of them.

## The SPICE Toolkit's translation is a tool of its own

What the translator is told about the toolkit, and what is done to the
translation afterwards, is in `tools/spice-translation` and not in
Fortrania: the translator knows FORTRAN, and the tool knows SPICE. Its
settings name the toolkit's libraries as two projects, the libraries
themselves and their test program, and read the sources from `cache/` at
the repository root, which git ignores and the settings fill from the
NAIF's server, checked by hash, when it is not there.

**The toolkit checks line endings itself, and `ZZPLATFM` decides the
check.** The runtime reads a text file whatever its line ending, as
*Text in sequence is lines, and bytes* above has it. The toolkit does not
leave this to the runtime: on loading a text kernel it reads the file's
first bytes by direct access, works out which ending the file has, and
refuses the kernel when that is not the ending of the platform it says it
was built for. It says so from `ZZPLATFM`, a table of facts about the
platform, and the sources translated are the Unix variant, which expects a
line feed alone. On Windows a program ends the lines it writes with a
carriage return and a line feed, as the reference compiler's do there, so
every text kernel the test suite writes for itself was refused, and the
reference compiler's build of the same sources failed in exactly the same
places.

The toolkit's variants for each platform and compiler were compared,
routine by routine: of the 1,483 files of the main library and 216 of the
support library, 29 and 9 differ somewhere, and the test utilities are the
same everywhere. Apart from `ZZPLATFM`, what differs is limits, the
length of a file's name and how many text files are open at once, which
the variant translated already has at the larger value and the runtime
does not limit; a correction for characters whose code is negative, which
the runtime's never are; how a program writes to the screen and ends; and
the machine constants of a platform that stores numbers big-endian. So one
routine needs to know the platform, and no variant is needed.

One difference is not a limit. `STMP03` sums a series, and how many terms
it sums is a constant its own comment says how to choose: the fewest
after which one more changes nothing in double precision. The variant
translated sums 11, where for IEEE doubles 10 are enough and the eleventh
is some 10^-21 of the result; the Windows variant sums 10. The translation
keeps 11, which is what the reference compiler's build of the same
sources does, so that the two can be compared bit for bit. A result that
differs in its last bit from the Windows build's would differ the same way
between the toolkit's own builds.

**`ZZPLATFM` is rewritten to answer from the machine it runs on.** After
the translation, the tool replaces each fact the routine stores with a
choice made when it runs: the line ending from the context's own setting,
so that the check agrees with what the program writes, the byte order and
the operating system from .NET, and the compiler as Fortrania, not as the
compiler the sources name. The values chosen between are the toolkit's
own names for its platforms. The rewrite finds each store by its constant
and refuses when one is not there exactly once, so a new version of the
sources that changes the routine stops the translation instead of being
half rewritten. Supplying the routine by hand was the alternative; it
would be a second copy to keep right, and a hand-written file in the
translated component, whose licence is the toolkit's.

With that, the translated test suite runs all 400 families on Windows.
F_ZZASC2 then fails six cases: it is the test of the check itself, and the
Linux package's copy expects the line feed alone to be native, where the
Windows package's copy of the same test differs in exactly those six
expectations. The reference compiler's build of the same sources, with
`ZZPLATFM` told the same line ending, gives the same outcome for every
family and every case that fails.

**Every translated file carries the header NAIF's rules ask for.** The
tool adds, inside the block the translator marks as generated, that the
file is a machine translation of a routine of NASA's SPICE Toolkit, of
which version, that it is not a product of NASA, Caltech/JPL or NAIF and
that NAIF does not support it, and that the toolkit's
terms apply; a file a rewrite changed says what changed. A file that has
the header is left alone, so running the tool twice changes nothing.
