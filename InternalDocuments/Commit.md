# Commit message tool

Local Python script (`tools/create_message.py`) that builds a git commit message. It makes no server call.

```bash
python3 tools/create_message.py risk intention message
```

Prints `<risk char> <intention char> <message>` to stdout, e.g. `. e add create_message.py`.

* risk and intention are passed as full names (not the character codes); the script maps names to characters
* message: short message describing the change (required, non-empty)

risk:
* completely_safe: `.` does not break the production
* proven: `-` the change is completely covered by unit test
* single_item: `!` only doing one thing
* other: `@` all other risks

intention:
* environment: `e` files used to build the code but not actual code
* test: `t` test files
* refactor: `r` code refactor
* feature: `F` adding or changing code feature
* bug: `B` fixing bugs
* documentation: `d` creating documentation

validation / responses:
* unrecognized risk or intention name -> `error: ...` on stderr, exit code 1
* missing/empty message -> `error: message is required` on stderr, exit code 1
* wrong number of arguments -> usage on stderr, exit code 2
