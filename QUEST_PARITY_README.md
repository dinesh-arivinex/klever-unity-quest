# Quest parity test build

Open this project with Unity 2022.3.12f1 and select Android. This isolated working copy preserves the original Quest scene, Oculus SDK packages, loader, application identifier, architecture and controller/hand interactions.

Shared fixes ported from the DPVR work include authoritative completion status, loading/completion feedback, null-safe completion and MCQ cleanup, assessment listener rebinding using Oculus events, current/legacy lesson content schemas, and resetting video audio/direct-source state between clips. DPVR-specific 3DoF controls, guides and native decoder assets are excluded.

The Android launcher uses the existing Flutter Klever artwork, including legacy, round and adaptive icons. The build preprocessor reapplies those assets automatically. Use **Klever → Apply Flutter App Icons** to configure them manually.

For batch validation, use `-executeMethod Klever.VR.Editor.QuestParityAudit.Run`. To validate and build, use `-executeMethod Klever.VR.Editor.QuestParityBuild.BuildTestAndroid`. Optional environment variables: `KLEVER_ANDROID_SDK_ROOT` and `KLEVER_QUEST_BUILD_OUTPUT`. The default output is `Builds/Quest/parity01/Klever_Quest_Parity_Test.apk`.

The test pipeline temporarily uses debug signing and the separate package `com.klever.vr.quest.paritytest`, named Klever Quest Parity Test. Release identity and signing settings are restored after packaging. This avoids requiring the unavailable release-signing passwords and allows the UAT app to coexist with the released app. Use the normal Unity build flow and the owner's release key for an actual release update.

The editor audit invokes production code with local fixtures and the real scene; it does not publish learner answers or scores. Physical Quest controller/hand, audio/video, completion and launcher appearance checks remain required. No Quest is currently available for that UAT.

Full parity decisions and verification evidence are in the DPVR workspace `QUEST_PARITY_AND_BRANDING.md` and `evidence/QUEST_PARITY_20261107.log`.
