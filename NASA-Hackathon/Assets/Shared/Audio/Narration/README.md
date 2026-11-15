# Offline scene narration

The saved build scenes contain assigned Brian voice clips. No API key or network connection is needed in Play mode or on Quest.

- TitleScreen-Night: opening tagline automatically; point the UI ray at a mission button to hear its card.
- TitleScreen: the opening story panel plays automatically.
- Opportunity-Arrival: story beats play when shown; select a rover hotspot to hear it.
- Opportunity-Flashback: the existing five Headquarters narration clips remain assigned.
- Opportunity-Launch and InSight-Launch: the briefing plays on arrival; the launch button starts the countdown and flight narration. The final explanation finishes before the scene handoff.
- Opportunity-Landing: the introduction plays after landing; investigate the three rocks and choose a conclusion to hear their explanations.
- Opportunity-Legacy: each story beat plays as its panel appears, including the wake-up event progression.

Brian's voice ID is case-sensitive: `nPczCjzI2devNBz1zQrb`. New audio uses `eleven_multilingual_v2`. The Editor generator keeps credentials in memory only. Missing narration can be generated with **Last Signal > Narration > ElevenLabs Brian - All Scenes** using a TTS-enabled key and a budget within the account's available credits. Existing assignments are kept.

Use **Last Signal > Narration > Validate All Scene Audio** to check every enabled build scene for clip assignments, non-silent audio data, and an active listener. **Verify Playback in All Scenes** enters Play mode, exercises the narration triggers and launch handoffs, checks advancing playback and nonzero mixer output, then exits Play mode. Reports are written under `Library/LastSignalNarration`, outside versioned assets.

See [NASA-Sources.md](NASA-Sources.md) for the NASA references behind the mission history and science. The room, interaction prompts, and compressed countdown are educational presentation elements.
