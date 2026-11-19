# Variances

Where translated code or the runtime knowingly behaves otherwise than the
reference: the reference compiler, for what the language leaves to a
compiler, and the reference toolkit, for what it does. Everywhere else the
aim is the same behaviour, and a difference not listed here is a fault.

The list is the whole of them. A change that makes another adds its row in
the same change. [`PENDING.md`](PENDING.md), *A setting for each variance
from the reference compiler*, has what is to become of the rows below: each
is to get a setting that turns the reference's behaviour back on, when a
program is met that needs it.

## The compiler's library

| Where | The reference compiler | Here | Why |
|---|---|---|---|
| `CALL SYSTEM` | has the operating system run the command | runs nothing and gives status -1, unless the context's `AllowsCommands` is set | a library is not to start programs on its host's behalf unasked |
| `IARGC` and `GETARG` | give the command line of the process | give the command line the context was told, none unless it was told one | the process's command line is its host's, not the library's |

## Input and output

| Where | The reference compiler | Here | Why |
|---|---|---|---|
| A `READ` or `WRITE` on a unit that is not connected | connects the unit to a file it names itself, `fort.` and the unit number, in the working directory | gives status 5001 | a library must not leave files where its host happens to run |
| A statement after one that failed | was seen to refuse a good `READ` of a record after a `WRITE` that overran another | is not affected by the earlier failure | it looks like an accident of that library and not like something to rely on |
| A `WRITE` to a file that may only be read | not measured | gives status 13, the operating system's number for it | a number had to be chosen; it is to be replaced by the measured one |
| A carriage return with no line feed after it, in text that is read | is part of the record | ends the record | a file made on any system is to be read on any other |
| Units 6, 0 and 5, while no file is opened on them | are the console | write nowhere and read nothing, until the host sets them or calls `UseConsole` | the console is the program's, and a program may hold many instances |
| A `WRITE` to a character variable that runs past its last record | leaves the record it was writing as far as it had come, and was seen to write over the first record with what came after the last | writes each record whole or not at all, and nothing past the last | a record is written when it is whole; what the variable holds after the failure is left open by the language |
| A `READ` from a character variable whose format moves past its last record before an item | gave status 0, and the item a value of blanks | gives status -1, the end of the file | the end of the file is where the language puts it |
| A formatted `WRITE` that fails part of the way through its list | has written what came before the failure, with no line ending | writes nothing of the record | a record is written when it is whole |
