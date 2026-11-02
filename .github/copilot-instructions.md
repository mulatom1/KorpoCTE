<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit - Module 5, Lesson 5 (Async & Remote Agents)

Delegate a bounded, planned change and come back to review it. Control doesn't disappear; it moves to the task contract, the sandbox setup and the review at the end.

```
/10x-plan -> /goal + /10x-goal-implement (interactive or your AI coding assistant in non-interactive mode) -> review the commits and the manual checklist
```

### Task Router - Where to start

| Skill | Use it when |
| --- | --- |
| `/10x-goal-implement <change-id>` | An approved `context/changes/<id>/plan.md` should run unattended. Pair it with `/goal <condition>`; it commits each phase on green and ends with pending manual checks. |
| `/10x-implement` | You want to steer each phase yourself. |

### Hard rules

- **Delegate only planned work.** No approved `plan.md`, no unattended run.
- **Bound every run.** The `/goal` condition names the finish state and a turn limit.
- **A green run is not a finished change.** Review the commits and work through the manual checklist before merging.
- **Give the sandbox only the secrets and network access the task needs.**
- **Watch parallel and remote runs from one place.** Run each unattended session in its own herdr workspace so you can see which agents are working, blocked or done and step in from the browser (setup in the M2L6 Deep Dive).

<!-- END @przeprogramowani/10x-cli -->
