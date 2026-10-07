# Unity Project Coding Rules

## General

- This project is a Unity 3D horror game.
- Use C# and follow standard Unity conventions.
- Do not introduce external packages unless explicitly requested.

## Comments

- Write comments in Japanese.
- When creating a new script, add comments explaining the purpose of the class.
- Add comments above important methods explaining what they do.
- Add comments for logic that may be difficult to understand later.
- Add comments explaining Unity-specific behavior such as Update, Coroutine, Raycast, Collider, and Cinemachine interactions.
- Do not comment every obvious line.
- Prioritize comments explaining "why" the code exists rather than simply repeating "what" the code does.

Example:

```csharp
// プレイヤーが1人称視点のときだけ青年を表示する
if (isFirstPerson)
{
    monsterRenderer.enabled = true;
}
```
