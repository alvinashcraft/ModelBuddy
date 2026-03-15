# Privacy Policy — Model Buddy

**Last updated:** March 15, 2026

Alvin Ashcraft / Alvinitech ("we", "us", or "our") built Model Buddy as a free, open-source Windows desktop application. This Privacy Policy explains how Model Buddy handles — and, more importantly, does **not** handle — your data.

## Summary

Model Buddy does not collect, transmit, or store any personal data. Everything stays on your device.

## Data Collection

**We do not collect any data.** Model Buddy does not include analytics, telemetry, crash reporting, or any other mechanism that sends information from your device to us or to third parties.

## AI Processing

All AI model inference is performed **locally on your device** through Microsoft Foundry Local. Your conversations, prompts, and model responses never leave your machine. No data is sent to cloud-based AI services.

## Local Storage

Model Buddy stores a small number of user preferences on your device using the standard Windows application settings API (`ApplicationData.Current.LocalSettings`). These include:

- Your selected app theme (Light, Dark, or System)
- Custom system instructions for the AI assistant
- A custom Foundry Local endpoint URL, if configured
- The ID of your most recently selected chat model

This data is stored locally in your Windows user profile and is never transmitted anywhere. You can reset these preferences at any time from the Settings page, or by uninstalling the app.

## Chat History

Chat conversations are held in memory only for the duration of your session. When you close Model Buddy or clear the chat, your conversation history is permanently discarded. Chat messages are never written to disk or sent to any external service.

## Network Activity

Model Buddy communicates **only** with a Foundry Local instance running on your local machine (typically at `http://127.0.0.1:5272`). This communication includes:

- Checking whether the Foundry Local service is running
- Retrieving the model catalog and model status
- Downloading AI models from Microsoft's model distribution infrastructure (initiated through Foundry Local)
- Sending chat messages to a locally loaded model and receiving responses

Model Buddy does not contact any other servers, APIs, or endpoints. The app does not access the internet directly — all model downloads are managed by the Foundry Local service.

## Third-Party Services

Model Buddy does not integrate with any third-party analytics, advertising, or tracking services. The only external dependency is Microsoft Foundry Local, which is a separate application governed by its own terms and privacy policy.

## Children's Privacy

Model Buddy does not knowingly collect any information from anyone, including children under the age of 13.

## Microsoft Store

If you install Model Buddy from the Microsoft Store, the Store itself may collect standard telemetry as described in [Microsoft's Privacy Statement](https://privacy.microsoft.com/privacystatement). This is outside the control of Model Buddy and Alvinitech.

## Open Source

Model Buddy is open source. You can review the complete source code to verify these privacy practices at [https://github.com/alvinashcraft/ModelBuddy](https://github.com/alvinashcraft/ModelBuddy).

## Changes to This Policy

If we make changes to this Privacy Policy, we will update the "Last updated" date at the top of this page. Continued use of Model Buddy after any changes constitutes acceptance of the updated policy.

## Contact

If you have questions about this Privacy Policy, you can reach us at:

- **Email:** [alvin@alvinashcraft.com](mailto:alvin@alvinashcraft.com)
- **GitHub:** [https://github.com/alvinashcraft/ModelBuddy/issues](https://github.com/alvinashcraft/ModelBuddy/issues)
