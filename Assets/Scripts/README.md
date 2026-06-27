# Assets/Scripts — โครงสร้าง

```
Assets/Scripts/
├── Common/                      ← ใช้ซ้ำข้ามฉาก/ภูมิภาค
│   ├── SceneFadeController.cs
│   ├── SwaySprite.cs
│   ├── MarkerBob.cs
│   ├── RevealPopShake.cs
│   ├── Movement/
│   │   ├── PatrolWalk.cs
│   │   ├── QuestAutoWalker.cs
│   │   └── QuestProximityReveal.cs
│   └── EditorGizmos/             ← MonoBehaviour ที่วาด gizmo ช่วยจัดวางใน Editor
│       ├── PathGizmoDrawer.cs
│       └── WaypointGizmo.cs
│
├── WorldMap/                     ← แผนที่โลก / การปลดล็อก region
│   ├── WorldMapProblemIslands.cs
│   ├── WorldMapFadeIn.cs
│   └── OwlGuideAnimator.cs
│
└── Region1/                      ← ทุกอย่างของภูมิภาค 1
    ├── Cutscenes/                ← คัตซีน
    │   ├── BearCutsceneEntrance.cs
    │   ├── OwlGreetingCutscene.cs
    │   ├── CutScene2ChaseController.cs
    │   ├── CrowCutsceneController.cs
    │   ├── MagicStonePuzzleController.cs
    │   └── MagicStonePuzzleStone.cs
    ├── ReferenceForest/          ← แผนที่ป่าหลัก + เควส
    │   ├── QuestPathSequence.cs
    │   └── NightLighting.cs
    ├── QuestMap/                 ← ฉากแผนที่เควส
    │   └── QuestMapSimpleCharacterWalk.cs
    ├── Success/                  ← ฉากผ่านเควส
    │   └── SuccessSceneIntro.cs
    └── Adventure/                ← เกมต่อคำ (data-driven)
        ├── Core/                 ← ตรรกะล้วน (C# บริสุทธิ์, มี unit test)
        ├── Data/                 ← ScriptableObject (เนื้อหาเควส)
        ├── Net/                  ← เชื่อมต่อ backend
        ├── View/                 ← UI / การแสดงผล
        └── BearEncounterFlow.cs
```

---

## Common/ — ใช้ซ้ำข้ามฉาก/ภูมิภาค
| ไฟล์ | หน้าที่ |
|------|--------|
| `SceneFadeController.cs` | overlay fade ดำ reusable: `Cover()` ค่อยๆ มืดก่อนเปลี่ยนฉาก + reveal ตอนเข้าฉากป่า; `RevealComplete` ให้ gameplay รอจน fade เสร็จ |
| `SwaySprite.cs` | ต้นไม้/พืชไหวเบาๆ ด้วย sine wave (รันเฉพาะ Play mode, สุ่ม phase ได้) |
| `MarkerBob.cs` | เครื่องหมาย "!" ลอยขึ้น-ลงอยู่กับที่ (ไม่ override ตำแหน่งที่วาง) |
| `RevealPopShake.cs` | เอฟเฟกต์ reveal: fade-in + สั่นแบบเด้ง (phone-buzz) |

### Common/Movement/ — การเคลื่อนที่
| ไฟล์ | หน้าที่ |
|------|--------|
| `PatrolWalk.cs` | สัตว์เดินไป-กลับในคอกตามแกน, ตั้งระยะแต่ละฝั่งแยกได้, ขับ walk animation ตามทิศจริง |
| `QuestAutoWalker.cs` | เดินตัวละครเข้าหา quest item ที่ใกล้ที่สุดแล้วหยุดเมื่อถึงระยะ |
| `QuestProximityReveal.cs` | ซ่อนลูกๆ ไว้ แล้วเผยทีละตัวเมื่อผู้เล่นเดินเข้าใกล้ |

### Common/EditorGizmos/ — ตัวช่วยจัดวางใน Editor
| ไฟล์ | หน้าที่ |
|------|--------|
| `PathGizmoDrawer.cs` | วาด gizmo เส้น path (จุด + เส้นเชื่อม) ช่วยจัด waypoint |
| `WaypointGizmo.cs` | วาด gizmo จุด waypoint เดี่ยว |

## WorldMap/ — แผนที่โลก
| ไฟล์ | หน้าที่ |
|------|--------|
| `WorldMapProblemIslands.cs` | ระบบเกาะ/ปลดล็อก region บนแผนที่ (เก็บ progress ใน PlayerPrefs) |
| `WorldMapFadeIn.cs` | fade-in ตอนเข้า WorldMap + flag `IsFadeBlocking` กันอินพุตระหว่าง fade |
| `OwlGuideAnimator.cs` | แอนิเมชันนกฮูกผู้นำทาง (ใช้กับ prefab `OwlGuide`) |

## Region1/Cutscenes/ — คัตซีนภูมิภาค 1
| ไฟล์ | หน้าที่ |
|------|--------|
| `BearCutsceneEntrance.cs` | หมีเดินเข้าฉากตาม waypoints แบบ cinematic (โค้งกระโดด, สลับท่า, ค่อยๆ ใหญ่) — ใช้ cut_scene1/2/3 |
| `OwlGreetingCutscene.cs` | แอนิเมชันนกฮูกทักทายแบบ frame-by-frame — cut_scene1 |
| `CutScene2ChaseController.cs` | ฉากหมีไล่ + white fade — cut_scene2 |
| `CrowCutsceneController.cs` | อีกาบินในฉาก + black fade — cut_scene3 |
| `MagicStonePuzzleController.cs` | ปริศนาลากหินวางช่อง + อัดเสียง/เข้ารหัส WAV แล้วไปฉากถัดไป — cut_scene1 |
| `MagicStonePuzzleStone.cs` | หินแต่ละก้อนที่ลากได้ (drag handler, จำตำแหน่งเดิม) คู่กับ controller |

## Region1/ReferenceForest/ — แผนที่ป่าหลัก
| ไฟล์ | หน้าที่ |
|------|--------|
| `QuestPathSequence.cs` | **สคริปต์หลักของเควส**: เดินตาม waypoint → reveal NPC/marker → คลิก → fade → เดินต่อ |
| `NightLighting.cs` | overlay กลางคืนแบบ stylized คุมด้วยค่า 0..1 (QuestPathSequence เป็นคน fade เข้า) |

## Region1/QuestMap/
| ไฟล์ | หน้าที่ |
|------|--------|
| `QuestMapSimpleCharacterWalk.cs` | ตัวละครเดิน (bob/sway) ในฉาก quest_map1 |

## Region1/Success/
| ไฟล์ | หน้าที่ |
|------|--------|
| `SuccessSceneIntro.cs` | intro ฉาก success: เสียง + black fade + อัปเดต progress แผนที่ |

## Region1/Adventure/ — เกมต่อคำ (data-driven)
ไฟล์ราก:
| ไฟล์ | หน้าที่ |
|------|--------|
| `BearEncounterFlow.cs` | flag ส่งต่อข้ามฉาก (reference_forest ↔ word_build) |

### Adventure/Core/ — ตรรกะล้วน (C# บริสุทธิ์, มี unit test)
| ไฟล์ | หน้าที่ |
|------|--------|
| `EncounterModel.cs` | state การวาง tile (tray ↔ slot) |
| `OutcomeEvaluator.cs` | ตรวจคำที่สร้างว่าถูก/ผิด (ตัดสินกิ่งเกม) |
| `Outcome.cs` | enum: `Correct / WrongWord / NonWord` |
| `BuildLatencyTracker.cs` | จับเวลาช่วง build (วัด cognition เป็น ms) |
| `TelemetryQueue.cs` | คิว telemetry FIFO + retry/backoff |
| `WavEncoder.cs` | เข้ารหัส WAV 16-bit PCM (reusable, testable) |
| `AvHelpers.cs` | helper เล่นเสียง/ตั้งค่าภาพแบบ null-safe (รันได้แม้ไม่มี asset) |

### Adventure/Data/ — ScriptableObject (เนื้อหาเควส)
| ไฟล์ | หน้าที่ |
|------|--------|
| `EncounterConfig.cs` | แหล่งข้อมูลตั้งค่า 1 encounter |
| `WordDatabase.cs` | รายการคำทั้งหมดของ Region 1 (lookup ผลลัพธ์) |
| `WordEncounterData.cs` | ข้อมูลคำ + craft effect ตอนสร้างสำเร็จ |
| `StoneTileData.cs` | ข้อมูล 1 tile (phoneme/grapheme) |
| `CutsceneData.cs` | ข้อมูล cutscene (ลิสต์ frame, ไม่มีข้อความบนจอ) |

### Adventure/Net/ — เชื่อมต่อ backend
| ไฟล์ | หน้าที่ |
|------|--------|
| `GradeApiClient.cs` | อัดเสียง mic → WAV 16kHz → POST `/grade` (fire-and-forget) |
| `GradeResponse.cs` | โครงสร้าง response จาก `/grade` (สำหรับ debug HUD) |
| `TtsApiClient.cs` | ดึงเสียงพากย์จาก `/tts` + cache ตาม lineId |
| `TelemetryClient.cs` | sink telemetry แบบ durable (journal ลง disk + pump ส่ง) |
| `SessionContext.cs` | เปิด session ตอนเริ่ม / ปิดตอนออก (รวมข้อมูลให้ dashboard) |

### Adventure/View/ — UI / การแสดงผล
| ไฟล์ | หน้าที่ |
|------|--------|
| `WordBuildEncounterController.cs` | **ตัวขับ encounter หลัก**: intro → ต่อคำจาก tile → confirm → echo (นกฮูกพูด) → mic → ยิง /grade เงียบๆ → แตกกิ่งตามผลคำ |
| `CutscenePlayer.cs` | เล่น `CutsceneData` แบบ visual-novel (เสียง+ภาพ ไม่มีข้อความ, auto-advance) |
| `StoneTile.cs` | tile ในถาด — ส่ง event ตอน tap/hover |
| `TileSlot.cs` | ช่องวาง tile — ส่ง event ตอน tap เพื่อคืน tile |

---

## Assets/Editor/ — เครื่องมือ Editor (one-shot builders / utilities)
สคริปต์เหล่านี้อยู่นอก `Assets/Scripts/` (โฟลเดอร์ `Assets/Editor/`) ไม่ได้รันตอนเล่นเกม แต่เป็นเครื่องมือใน Unity Editor เรียกผ่านเมนูบาร์ ใช้สร้าง/นำเข้า asset และประกอบฉากแบบกดครั้งเดียว

| ไฟล์ | เมนู | หน้าที่ |
|------|------|--------|
| `MapLayoutBuilder.cs` | `Tools/Reference Map/Build From Layout` | อ่าน `map_layout.json` แล้วประกอบฉากป่า: paint tilemap (L0_Ground/L1_Water/L2_Path), วาง object (L3–L6), สร้าง quest waypoint — มี guard เมื่อไม่พบ tilemap หรือ asset id ไม่รู้จัก |
| `MiniJson.cs` | — | JSON parser/encoder (MIT, Calvin Rien) แปลงเป็น `List<object>`/`Dictionary<string,object>` ไม่ throw error — ใช้โดย `MapLayoutBuilder` |
| `ForestAnimationSetup.cs` | `Tools/Forest/*` | ชุดเครื่องมือฉากป่า: Animate Pigs, Add Sway To Plants, Diagnose Water, Import And Prefab Trees, Dump Tree Rects, Rename Tree Prefabs, Animate Pigs Foxes Fire — สร้าง animation clip/controller และนำเข้า prefab ต้นไม้ |
| `GifPlaceablePrefabGenerator.cs` | `Tools/NSC/GIF Placeables/*` | แปลง sprite จาก `Super_Retro_Collection` เป็น prefab วางได้ (Generate Object Prefabs / Generate Every Sprite Prefab) + เขียน report; ข้าม tile palette/sample |
| `IslandObjectPrefabBuilder.cs` | `Tools/Quest/*` | Build Island Object Prefabs (PNG → SpriteRenderer prefab) และ Build Quest Marker (Animated) — เครื่องหมาย "!" แบบ Shadow + Mark ที่เด้งขึ้น-ลง |
| `ReferenceMatchLayoutBuilder.cs` | `NSC Tools/Build Reference Match Layout` | วาง layout ต้นไม้/สะพาน อ้างอิงฉากต้นแบบ `real_region1` ลงใต้ root `REFERENCE_MATCH_LAYOUT` |
