**ALWAYS** start every reply with 🍀 + space. This is a per-message rule, not a one-time setting — check it before sending every single reply, including short ones and replies under concise/terse output modes (brevity means shorter sentences, not dropping this prefix).\

It is important that we both understand together. If anything in my prompt is ambiguous or underspecified, ask before acting on a guess. 
Ask one question at a time, not a batch.

At the end of every task (not just complex ones), report on all of these, briefly:
- Skills used: name them.
- Skill gaps: if a skill you wished existed would have helped, propose one, even briefly.
- Prompt feedback: one concrete way I could have asked more effectively this time — skip it only if there's genuinely nothing worth saying.
- Questions, ideas, concerns: surface them as they come up, not just at the end.

Treat this end-of-task report as mandatory, like a checklist item — don't let it get crowded out by concise-output-style guidance; it's a small fixed addition, not a violation of terseness.


Never use Claude Code's persistent memory system for this project. Durable guidance (conventions, process corrections, preferences) belongs in-repo instead — this file, `InternalDocuments/` - so it's visible in version control and to every contributor, not just stored privately for one user.

## Decisions
When you need input on a decision:
- First, pick the option you think is best and propose it. Start that message with `⭐ 1: `
- Also list the alternatives you rejected, each with a brief reason why. Start each with `❌ n:`
Then have a discussion.
This formatting applies every time you present a decision, even a quick one — don't let a short or informal-feeling exchange skip it.

## Markdown
Numbered lists in markdown files prefix every item with `1.`, not an incrementing sequence.

## Commit messages
Use `tools/create_message.py` to build every commit message in this repo. Run `python3 tools/create_message.py <risk> <intention> "<message>"` and use its stdout as the commit message. Pass full names, not character codes. Risk is one of `completely_safe` (`.`, can't break production), `proven` (`-`, fully covered by unit tests), `single_item` (`!`, does only one thing) or `other` (`@`, everything else). Intention is one of `environment` (`e`, build files), `test` (`t`), `refactor` (`r`), `feature` (`F`), `bug` (`B`) or `documentation` (`d`). The output looks like `-F add login`. An unrecognized name or an empty message prints an error to stderr and exits non-zero, so fix the arguments and rerun instead of writing the message by hand.

