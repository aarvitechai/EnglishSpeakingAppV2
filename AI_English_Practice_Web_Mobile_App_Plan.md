# AI English Speaking Practice Platform — Web & Mobile App Plan

**Product blueprint, technical architecture, phased roadmap, and launch checklist**

**Working product name:** SpeakMate AI (placeholder — choose a final name after checking trademarks and domain availability)  
**Product:** An AI-powered English speaking and communication practice platform for learners worldwide  
**Platforms:** Responsive web app + Android and iOS mobile apps  
**AI provider:** Google Gemini API through a secure backend  
**Primary goal:** Help learners practise speaking regularly with an AI conversation partner, receive useful feedback, and track measurable progress.

---

## 1. Executive summary

Build a friendly, accessible product where a learner can open the app, choose a level and goal, speak with an AI tutor, receive understandable feedback, and continue practising with a personalised learning path.

The product should not be only a chatbot. Its core value is a complete learning loop:

1. **Set a goal:** everyday conversation, work, interviews, travel, study, pronunciation clarity or confidence.
2. **Practise:** talk naturally with an AI partner using voice.
3. **Understand:** receive a transcript and selected corrections.
4. **Improve:** repeat better versions and practise useful phrases.
5. **Measure:** see session history, recurring mistakes and progress over time.
6. **Return:** receive a manageable next practice activity.

### Product promise

"Practise speaking English whenever you want, get supportive feedback, and see your progress."

### Product principles

- Speaking-first: voice conversation is the central experience.
- Beginner-friendly: plain language, gentle correction and adjustable difficulty.
- Useful feedback: prioritise a few important improvements, not every tiny mistake.
- Trustworthy: be clear when speech recognition or AI feedback may be uncertain.
- Inclusive: design for different first languages, abilities, devices and connection quality.
- Safe by default: protect recordings, transcripts, account information and API credentials.
- Sustainable: control AI usage and operating costs before opening access to everyone.

---

## 2. Audience and user problems

### Primary audiences

1. **Beginners:** understand some English but hesitate to speak.
2. **Students:** need conversation, presentations, exams or study support.
3. **Job seekers and professionals:** need interviews, meetings, workplace conversation and presentations.
4. **Travellers:** need practical everyday communication.
5. **Independent learners:** want regular speaking practice without scheduling a human partner.

Do not assume every learner has the same goal, first language, accent or confidence level.

### Problems to solve

- Learners have nobody available to practise with.
- They feel embarrassed making mistakes in front of people.
- They know grammar and vocabulary but cannot respond quickly.
- They do not know which mistakes to focus on.
- They practise irregularly and cannot see improvement.
- Some tools provide corrections but do not create a natural conversation.

### Initial target

Start with adult learners who want practical spoken English. Add specialised courses and age-specific experiences only after safety, moderation and product requirements are designed.

---

## 3. MVP: what to build first

Keep the first release focused. The MVP should prove that users return because speaking practice and feedback are genuinely useful.

### Must-have features

| Feature | MVP behaviour |
|---|---|
| Sign up / sign in | Email authentication; guest demo can be considered |
| Onboarding | Ask level, goal, preferred explanation language and daily target |
| AI voice practice | Start, pause/end and continue a voice conversation |
| Text fallback | Let users type when microphone, permissions or network are unavailable |
| Conversation modes | Free conversation, daily topics and simple role-play |
| Level adaptation | Beginner, intermediate and advanced language complexity |
| Transcript | Show the conversation text where supported and consented |
| Feedback | Corrected examples, top 3 priorities and useful phrases |
| Practice history | Date, duration, topic and summary |
| Progress | Sessions completed, practice minutes and self-improvement indicators |
| Settings and privacy | Explain data use; allow account and data controls |

### Do not build in the first release

- Public social network or open chat rooms.
- Live human tutoring marketplace.
- Competitive leaderboards.
- Complex exams or claims of official CEFR certification.
- Dozens of AI personas and languages.
- Expensive real-time voice features before usage economics are tested.
- Unreviewed community content or user-to-user messaging.

### MVP success criteria

Validate with a small group of real learners. Suggested early signals:
- Users can complete onboarding without help.
- A new user can start a practice session quickly.
- Voice and text fallback both work.
- Feedback is understandable and actionable.
- Users voluntarily return for another session.
- AI cost per active learner is measured and within a chosen budget.
- No API key is exposed in browser or mobile application bundles.

Set numeric targets after a pilot baseline; do not invent success claims before measuring.

---

## 4. User journey and core screens

### Main learner journey

1. Landing page explains who the app helps and what a practice session looks like.
2. User creates an account or tries a limited demo.
3. Onboarding captures level, goals and preferred feedback language.
4. Home screen recommends one short practice.
5. User chooses a mode and starts voice or text practice.
6. AI conducts a conversation one turn at a time.
7. Session ends with transcript (if enabled), corrections, phrases and next steps.
8. Progress page shows trends and recommends the next session.

### Screens

**Web**
- Landing / pricing / FAQ / privacy
- Sign up / sign in
- Onboarding
- Dashboard
- Practice setup
- Live conversation
- Session summary
- Progress and history
- Profile, settings and privacy controls
- Help and feedback

**Mobile**
- Welcome / authentication
- Onboarding
- Home
- Practice mode selection
- Voice practice screen
- Session result
- Progress
- Profile and settings

Design mobile-first. Keep the microphone state, connection state, mute/end controls and privacy notice clear. Never make the learner guess whether recording or transmission is active.

---

## 5. Learning experience and AI behaviour

### Conversation modes for launch

1. **Daily conversation:** familiar topics and follow-up questions.
2. **Role-play:** restaurant, shopping, travel, appointments and workplace scenarios.
3. **Interview practice:** job or study interview questions with feedback.
4. **Topic challenge:** speak for 30, 60 or 120 seconds.
5. **Guided beginner practice:** short prompts, simpler vocabulary and slower pacing.
6. **Text practice:** same learning structure without voice.

### Session structure

- Before: choose goal, level, topic and session length.
- During: AI asks one question at a time; gives the learner time to finish.
- After: show a concise summary, 1–3 priority corrections, useful expressions and one repeat task.
- Next time: revisit one recurring issue and introduce a small new challenge.

### Feedback design

Separate different kinds of feedback:
- Grammar and sentence structure.
- Vocabulary and natural phrasing.
- Fluency indicators such as long pauses or repeated filler words, only when reliably measurable.
- Pronunciation clarity and sound-level feedback, only when the speech pipeline supports it accurately.

Avoid presenting an AI-generated accent score as an objective truth. Do not claim to assess every phoneme accurately unless validated. Explain uncertainty and let users replay or edit transcripts where supported.

### Example AI coach system instruction

> You are a patient, encouraging English conversation coach. Adapt to the learner's selected level and goal. Ask one question at a time. Let the learner finish. Keep the conversation natural and relevant. Do not interrupt for minor mistakes. After a suitable interval or at session end, give no more than three high-priority corrections, show natural alternatives, explain briefly in the learner's selected support language when requested, and invite the learner to repeat or rephrase. Do not shame the learner or claim an official proficiency score. If speech recognition is uncertain, ask for clarification rather than treating the transcript as certainly correct. Do not request sensitive personal information.

Treat prompts and model outputs as untrusted input/output: validate structured responses on the server and do not allow model text to control privileged application actions.

---

## 6. Recommended technology architecture

### Recommended stack

| Layer | Recommended choice | Purpose |
|---|---|---|
| Web app | React + TypeScript + Vite or Next.js | Responsive browser experience |
| Mobile app | React Native + Expo + TypeScript | Shared mobile codebase for Android and iOS |
| Backend | Node.js + TypeScript + Express or NestJS | Authenticated APIs, AI orchestration and business rules |
| AI | Google Gemini API via official supported SDK | Conversation and feedback generation |
| Database | PostgreSQL (managed service such as Supabase or equivalent) | Users, preferences, sessions and progress |
| Authentication | Managed auth provider or secure backend auth | Account lifecycle and sessions |
| File/object storage | Private object storage, only if audio storage is required | Optional user-approved audio assets |
| Hosting | Managed web hosting + backend hosting | Deploy web and API |
| Monitoring | Error tracking, structured logs and usage metrics | Reliability and cost visibility |
| Source control | Git + GitHub | Versioning and collaboration |

This is a suggested architecture, not a requirement to use every service. Choose services based on region, pricing, privacy terms, operational skill and exportability.

### High-level architecture

```text
Web browser (React)             iOS / Android (React Native)
          |                                  |
          +----------- HTTPS ----------------+
                             |
                    Backend API (Node)
                             |
          +------------------+-------------------+
          |                  |                   |
      Auth/session       Learning logic       Usage limits
          |                  |                   |
          +------------------+-------------------+
                             |
                    Gemini API (server-side)
                             |
                    Validated AI response
                             |
                  PostgreSQL / session history
```

### Critical security rule

The Gemini API key must exist only on the server. Never put it in:
- React source code or browser bundles.
- Mobile application source or app configuration shipped to users.
- A variable with a public frontend prefix.
- Git commits, screenshots, logs or client error messages.

Use server-side environment secrets, rotate exposed keys, apply rate limits, and restrict access to authenticated users. The client calls your backend; your backend calls Gemini.

### Voice architecture decision

Prototype two paths before committing:

**Path A — speech-to-text + model response + text-to-speech**
- Capture the learner's speech with explicit microphone permission.
- Convert speech to text using an appropriate speech recognition service or supported platform capability.
- Send text and conversation context to the backend.
- Generate the AI response.
- Convert response text to speech.
- Display transcript and allow correction.

Advantages: simpler transcript and turn-based feedback; easier MVP cost controls.  
Limitations: latency, transcription errors, less natural interruption handling, separate speech services and possible additional costs.

**Path B — real-time audio conversation**
- Use a supported real-time audio interface if available and appropriate.
- Keep credential issuance and privileged orchestration on the backend.
- Follow provider requirements for short-lived client credentials, session configuration and allowed origins.
- Build reconnect, timeout, mute and end-session handling.

Advantages: potentially more natural low-latency interaction.  
Limitations: more complex security, session lifecycle, cost and debugging.

**Recommendation:** build a working text conversation first, then a turn-based voice prototype, then compare it with a real-time option. Do not promise real-time voice until it has been tested on target devices and networks.

---

## 7. Data model: start small

Suggested core tables:

- `users`: authentication-provider ID, created date, account status.
- `learner_profiles`: level, goals, support language, daily target and preferences.
- `practice_sessions`: user ID, mode, topic, start/end times, status and duration.
- `conversation_turns`: session ID, speaker, text, timestamps and optional confidence metadata.
- `session_feedback`: session ID, summary, priority corrections and useful phrases.
- `learning_goals`: user ID, goal, target and status.
- `usage_events`: user ID, model, token/audio usage, estimated cost and request status.
- `consent_records`: consent type, policy version, timestamp and withdrawal status.

Use migrations and foreign keys. Minimise personal data. Store raw audio only when there is a clear product need, explicit consent, a retention policy and secure deletion. Prefer not to store raw audio in the MVP.

---

## 8. Privacy, safety and trust

This product may process voice recordings, transcripts and information users say aloud. Treat these as sensitive user content even when they are not formally classified as sensitive by a particular law.

Required product work:
- Clear microphone permission explanation before first use.
- Clear indicator when microphone capture is active.
- Explain what is sent to AI providers, what is stored and for how long.
- Separate consent for optional audio storage or product improvement.
- Provide deletion and account closure flows.
- Encrypt traffic in transit and use managed encryption at rest.
- Restrict staff access; audit administrative access.
- Avoid logging raw conversation content by default.
- Define retention and backup deletion policies.
- Review applicable privacy, consumer, child-safety and data-protection laws in every launch market.
- Add abuse prevention, prompt-injection resistance, moderation and reporting processes.
- Do not market the service as a certified exam or medical/therapeutic service.

For a global product, obtain qualified legal and privacy review before launch, especially before serving children. Consider an adult-only initial pilot if child safety and parental consent flows are not ready.

### AI provider privacy and cost

Provider terms, model availability, free-tier eligibility, quotas, retention and data-use settings can change. Review the current official Google AI/Gemini terms and pricing before launch. Do not assume a free tier is suitable for commercial production or private user conversations. Avoid sending unnecessary personal data to the model.

---

## 9. Phased implementation roadmap

A realistic first build is a sequence of validated milestones, not one large coding task. Timelines below are planning estimates for a small team and will vary with experience and scope.

### Phase 0 — Product discovery and validation

**Deliverables**
- Choose product name, audience and initial launch geography.
- Interview 8–15 potential adult learners.
- Identify their top speaking goals and biggest practice barriers.
- Sketch user journey and low-fidelity screens.
- Define MVP, privacy assumptions and pilot success metrics.

**Exit check:** target user and core problem are specific; at least a few learners agree to test a prototype.

### Phase 1 — UX and technical foundation

**Deliverables**
- Create Git repository and project conventions.
- Set up TypeScript, linting, formatting and environment files.
- Create responsive web shell and mobile app shell.
- Build navigation, design tokens and reusable components.
- Set up dev, staging and production environments.

**Exit check:** web and mobile builds run; secrets are excluded from source control.

### Phase 2 — Accounts, onboarding and database

**Deliverables**
- Configure authentication.
- Build onboarding for level, goal and support language.
- Create database schema and migrations.
- Add account settings and consent records.
- Add basic server-side authorization.

**Exit check:** users can sign in, complete onboarding and access only their own data.

### Phase 3 — Text-first AI conversation

**Deliverables**
- Create backend Gemini service using the official supported SDK.
- Select a currently available model after checking Google AI Studio and current documentation.
- Build server-side prompt templates for level and practice mode.
- Validate model output and errors.
- Implement chat interface, context window and usage limits.
- Record session metadata and optional text transcript based on consent.

**Exit check:** learner can complete a useful text conversation; API key is never sent to clients; failures are handled gracefully.

### Phase 4 — Session feedback and learning history

**Deliverables**
- Generate concise session summary.
- Produce up to three priority corrections and useful phrases.
- Add practice history and basic progress view.
- Allow learner to retry corrected sentences.
- Add user feedback on whether a correction was helpful.

**Exit check:** feedback is understandable, editable where appropriate and linked to the correct session.

### Phase 5 — Voice MVP

**Deliverables**
- Prototype speech capture and transcription.
- Add text-to-speech response playback.
- Show microphone, listening, thinking and speaking states.
- Handle permissions, silence, network loss and transcription uncertainty.
- Add stop, retry and text fallback.
- Test on representative Android, iOS and desktop browsers.

**Exit check:** complete a voice practice session reliably on target devices and networks, with clear consent and controls.

### Phase 6 — Personalisation and progress

**Deliverables**
- Adapt topic difficulty and question complexity.
- Track recurring learner-selected priorities.
- Recommend short next sessions.
- Add weekly progress summaries with transparent indicators.
- Add reminders only with opt-in and user-controlled frequency.

**Exit check:** recommendations use real session history and can be reset or disabled.

### Phase 7 — Security, reliability and cost controls

**Deliverables**
- Rate limits, abuse prevention and request validation.
- Per-user quotas and spending alerts.
- Provider timeout, retry and fallback strategy.
- Error monitoring and operational dashboards.
- Privacy/deletion workflows and retention policies.
- Load, security and permission testing.

**Exit check:** no critical security findings; operating cost per session is known; incident response and support process exist.

### Phase 8 — Pilot and improvement

**Deliverables**
- Invite a small group of adult testers.
- Observe onboarding and session completion.
- Collect structured feedback and bug reports.
- Review retention, session completion, helpfulness and cost.
- Fix confusing screens and inaccurate feedback.
- Decide whether to expand audience or features.

**Exit check:** pilot evidence supports the product's value and the team can support users.

### Phase 9 — Public launch

**Deliverables**
- Production deployment and domain configuration.
- Store listings and mobile release process.
- Privacy policy, terms, support and contact channels.
- Analytics with data minimisation.
- Backup, monitoring and rollback plan.
- Launch communications and support workflow.

**Exit check:** production smoke tests pass, privacy controls work, support is ready and release owners approve launch.

---

## 10. Suggested repository structure

```text
speakmate-ai/
  apps/
    web/                 # React web app
    mobile/              # React Native + Expo app
    api/                 # Node.js backend
  packages/
    shared/              # Shared types, schemas and utilities
    ui/                  # Shared design tokens/components where practical
  docs/
    product-requirements.md
    architecture.md
    privacy-and-data-map.md
    api-contracts.md
    test-plan.md
  infra/
    deployment/
  .gitignore
  README.md
```

Keep provider SDK calls inside the backend. Share types and validation schemas where practical, but do not force identical UI components across web and mobile if their interaction patterns differ.

---

## 11. Testing strategy

### Functional tests
- Sign up, sign in, sign out and account recovery.
- Onboarding and profile updates.
- Start, pause, end and resume supported sessions.
- Text fallback and voice permissions.
- Transcript, feedback and history.
- Account data deletion and consent withdrawal.

### AI quality tests
- Beginner prompts stay beginner-friendly.
- Coach asks one question at a time.
- Feedback does not overwhelm the learner.
- Incorrect or uncertain transcription is not treated as certain.
- Model does not invent a proficiency certificate or guaranteed score.
- Prompt injection in user speech/text cannot reveal secrets or invoke privileged actions.
- Output schema validation rejects malformed responses.

### Device and network tests
- Android and iOS permission states.
- Desktop browser microphone permissions.
- Headphones, speaker playback and mute.
- Slow or interrupted network.
- Backgrounding the mobile app and reconnecting.
- Unsupported browsers and unavailable speech services.

### Security and privacy tests
- No API key in client bundles or source maps.
- Users cannot access other users' sessions.
- Rate limits work.
- Logs do not contain secrets or unnecessary raw voice content.
- Deletion and retention procedures work as documented.

---

## 12. Analytics and product metrics

Track only what is necessary and explain analytics use clearly.

| Metric | What it tells you |
|---|---|
| Onboarding completion | Is setup understandable? |
| First practice started | Do users reach the core value? |
| First session completed | Can users finish the main experience? |
| Repeat practice rate | Does the product encourage a habit? |
| Weekly active learners | Is usage sustained? |
| Feedback helpfulness | Are corrections useful? |
| Voice session failure rate | Is voice reliable? |
| Average AI cost per completed session | Is the business sustainable? |
| Support and safety reports | Where are users experiencing harm or confusion? |

Do not optimise for time spent alone. Optimise for useful learning outcomes, learner confidence and sustainable practice.

---

## 13. Monetisation options — decide after validation

Possible future model:
- Free tier with a clearly disclosed daily practice allowance.
- Paid subscription for higher usage, advanced scenarios and deeper progress tools.
- School or organisation plans with appropriate admin controls and privacy terms.

Before setting prices, measure actual AI, speech, storage, payment, support and hosting costs. Clearly explain limits and renewal terms. Do not promise unlimited AI usage if provider costs are variable.

---

## 14. Launch checklist

### Product
- [ ] Initial learner segment and supported languages are defined.
- [ ] MVP scope is frozen.
- [ ] Onboarding and core session flow tested with learners.
- [ ] Feedback is clear and not overwhelming.
- [ ] Text fallback exists.

### Engineering
- [ ] Web app deployed to staging.
- [ ] Android and iOS test builds available.
- [ ] Backend deployed separately from clients.
- [ ] Database migrations and backups configured.
- [ ] API keys stored only in server-side secrets.
- [ ] Rate limits, quotas and monitoring configured.

### Voice
- [ ] Microphone permission explained.
- [ ] Recording/listening state visible.
- [ ] Stop and retry controls work.
- [ ] Network interruption and fallback tested.
- [ ] Transcript uncertainty handled safely.

### Trust and operations
- [ ] Privacy policy and terms reviewed.
- [ ] Consent and deletion flows tested.
- [ ] Data retention policy documented.
- [ ] Support contact and incident process ready.
- [ ] Applicable legal and child-safety requirements reviewed.
- [ ] Cost per session and spending alerts verified.

---

## 15. First 14 working sessions: build order

Use this as the first implementation checklist. Do not start with advanced voice before text chat works.

1. Write the one-page product requirements and MVP exclusions.
2. Draw onboarding, home, practice and feedback wireframes.
3. Create the monorepo and TypeScript setup.
4. Build responsive web navigation and mobile navigation.
5. Create backend health endpoint and environment configuration.
6. Set up authentication and protected routes.
7. Create database schema and migrations.
8. Build onboarding and profile persistence.
9. Implement backend Gemini text endpoint with server-only key.
10. Build text chat UI and conversation history.
11. Add practice modes and level-aware prompt templates.
12. Add structured feedback and session summary.
13. Add basic usage limits, error handling and logs.
14. Test with 3–5 learners; fix blockers before starting voice integration.

---

## 16. Decisions to make before development

Use sensible defaults for the prototype, then confirm these decisions before public launch:

- Final product name and brand identity.
- Initial learner segment and launch countries.
- Supported interface languages for the first release.
- Whether first release is adult-only.
- Web/mobile feature parity requirements.
- Voice architecture: turn-based speech pipeline or real-time audio.
- Whether any audio is stored (recommended: no raw audio storage in MVP).
- Free usage allowance and paid plan, if any.
- Hosting region, privacy terms and data retention.
- Support and moderation responsibilities.

---

## 17. Official documentation to verify during implementation

Provider APIs, SDKs, models, quotas, pricing and voice capabilities change. Verify current documentation before writing integration code:

- Google AI for Developers: https://ai.google.dev/
- Gemini API documentation: https://ai.google.dev/gemini-api/docs
- Gemini API pricing: https://ai.google.dev/gemini-api/docs/pricing
- React: https://react.dev/
- React Native: https://reactnative.dev/
- Expo: https://docs.expo.dev/
- Node.js: https://nodejs.org/docs/
- PostgreSQL: https://www.postgresql.org/docs/

Choose the model and voice approach based on current availability, supported regions, privacy terms, latency, quality and measured cost. Do not hardcode an old model name from a tutorial.

---

## Final recommendation

Build **SpeakMate AI** in this order:

**Validate the learner problem → responsive web + mobile foundations → accounts and onboarding → text AI tutor → useful feedback → voice MVP → progress → security and cost controls → adult pilot → public launch.**

The most important product feature is not the number of AI features. It is a reliable daily practice loop that makes learners feel safe to speak, gives them a few useful improvements, and helps them come back tomorrow.
