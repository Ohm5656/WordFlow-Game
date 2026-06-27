# แผนภาพโครงสร้างโปรเจกต์ NSC-Game

เอกสารนี้สรุปจากโครงสร้างไฟล์ใน repo และสคริปต์หลักใน `Assets/Scripts/` โปรเจกต์นี้เป็น Unity project เวอร์ชัน `6000.4.3f1` ใช้ URP 2D, Unity Input System, UGUI และ TextMesh Pro

## ภาพรวมโครงสร้างโปรเจกต์

```mermaid
flowchart TD
    root["NSC-Game<br/>Unity project"]

    root --> assets["Assets/<br/>ไฟล์เกมหลัก"]
    root --> packages["Packages/<br/>manifest + lockfile"]
    root --> settings["ProjectSettings/<br/>ค่าตั้งค่า Unity / Build Settings"]
    root --> generated["Library / Temp / Logs / UserSettings<br/>ไฟล์ที่ Unity สร้างระหว่างทำงาน"]
    root --> vcs[".git / .plastic<br/>version control"]

    assets --> scenes["Scenes/<br/>WorldMap, region 1 scenes"]
    assets --> scripts["Scripts/<br/>C# gameplay scripts"]
    assets --> art["Art/<br/>ภาพ world map, quest map, visual novel"]
    assets --> sound["sound/<br/>เสียงพูด mp3"]
    assets --> render["Settings/<br/>URP 2D renderer assets"]
    assets --> thirdparty["TextMesh Pro / Teddy assets<br/>package และ asset ภายนอก"]
    assets --> recovery["_Recovery/<br/>scene recovery จาก Unity"]

    art --> worldnew["WorldMap New<br/>background + island region sprites"]
    art --> worldlock["WorldMap lock<br/>locked island parts 1-5"]
    art --> questart["quest_map<br/>background, bear, panel, stone"]
    art --> novelart["visaul_novel<br/>cutscene background, bear, owl, quest assets"]

    scenes --> world["WorldMap.unity"]
    scenes --> sample["SampleScene.unity"]
    scenes --> region["region 1/"]
    region --> quest["quest_map1.unity"]
    region --> cutscene["cut_scene1.unity"]
    region --> practice["practice.unity"]
```

## Flow ของ Scene หลัก

```mermaid
flowchart LR
    start["WorldMap.unity<br/>ผู้เล่นเลือกเกาะ"]
    quest["quest_map1.unity<br/>จุดภารกิจ: ป้าย + หินเวท + หมี"]
    cut["cut_scene1.unity<br/>หมีเข้าฉาก, นกฮูกพูด, เปิด puzzle"]
    practice["practice.unity<br/>ฉากถัดไปหลัง puzzle"]

    start -->|"WorldMapIslandIntroFlow<br/>LoadScene: quest_map1"| quest
    quest -->|"QuestPointInteractable<br/>LoadScene: cut_scene1"| cut
    cut -->|"MagicStonePuzzleController<br/>LoadScene: Assets/Scenes/region 1/practice.unity"| practice

    build["EditorBuildSettings.asset<br/>ยังอ้าง minigame1-1, minigame1-2, minigame1-3"]
    missing["หมายเหตุ: ตอนสำรวจไฟล์ ไม่พบไฟล์ scene เหล่านี้ใน Assets/Scenes"]
    build -.-> missing
    missing -.-> start
```

## ความสัมพันธ์ของไฟล์ Scripts

```mermaid
flowchart TD
    wmFade["WorldMapFadeIn.cs<br/>fade in ตอนเข้า WorldMap"]
    wmFlow["WorldMapIslandIntroFlow.cs<br/>ระบบ world map + island interaction"]

    qmFade["QuestMapFadeIn.cs<br/>fade in ตอนเข้า quest_map1"]
    qmSetup["QuestMapSceneSetup.cs<br/>bootstrap map_root"]
    qInteract["QuestPointInteractable.cs<br/>คลิกจุดภารกิจ"]

    bear["BearCutsceneEntrance.cs<br/>หมีเดินเข้า + reveal cutscene UI"]
    owl["OwlGreetingCutscene.cs<br/>นกฮูกพูด + focus + book reveal"]
    puzzle["MagicStonePuzzleController.cs<br/>logic puzzle หินเวท"]
    stone["MagicStonePuzzleStone.cs<br/>drag/drop หินแต่ละก้อน"]

    wmFade --> wmFlow
    wmFlow -->|"เลือกเกาะ playable"| qmFade
    wmFlow -->|"LoadScene quest_map1"| qmSetup
    qmSetup -->|"AddComponent + Initialize"| qInteract
    qInteract -->|"LoadScene cut_scene1"| bear
    bear -->|"เพิ่ม/เรียก PlayGreeting"| owl
    owl -->|"หา/เพิ่ม component"| puzzle
    puzzle -->|"สร้าง/ควบคุม stone components"| stone
    stone -->|"ส่ง event drag/release กลับ"| puzzle
    puzzle -->|"completion ritual + LoadScene"| practice["practice.unity"]
```

## อธิบายไฟล์ใน `Assets/Scripts/`

| ไฟล์ | หน้าที่หลัก |
|---|---|
| `WorldMapIslandIntroFlow.cs` | controller ใหญ่ของหน้า World Map: อ่านเกาะทั้งหมดที่ชื่อขึ้นต้น `Island`, จัดการ click/touch/drag/zoom/pinch, เลือกเกาะที่เล่นได้, ทำ fade ก่อนเปลี่ยน scene, จัด progression ด้วย `PlayerPrefs`, ทำ locked island overlay, path cue, focus mist, glow, bounce และ guide UI พร้อมเสียง/Android TTS |
| `WorldMapFadeIn.cs` | bootstrap หลังโหลด scene แล้วถ้า active scene คือ `WorldMap` จะสร้าง canvas สีดำเต็มจอและค่อย ๆ fade out เพื่อเปิดฉาก |
| `QuestMapSceneSetup.cs` | bootstrap สำหรับ `quest_map1`: หา `map_root`, หา child `panel ?`, `stone`, `bear_0`, แล้วเพิ่ม `QuestPointInteractable` ให้อัตโนมัติถ้ายังไม่มี |
| `QuestMapFadeIn.cs` | fade in สีดำสำหรับ `quest_map1` โดยสร้าง canvas runtime แล้วทำ alpha จาก 1 เป็น 0 |
| `QuestPointInteractable.cs` | logic จุดภารกิจในแผนที่ย่อย: ทำป้าย/หิน pulse พร้อม glow, ตรวจ mouse/touch บนป้าย หิน หรือหมี, bounce หมี, zoom camera ไปจุดภารกิจ, white flash แล้วโหลด `cut_scene1` |
| `BearCutsceneEntrance.cs` | cutscene ตอนเข้าฉาก: white fade out ตอนเริ่ม, ขยับหมีจาก waypoint sprites หรือ path ตรง, squash/stretch ตอนลงพื้น, เปิด shield ถ้ามี, fade in object ถัดไป แล้วเรียก `OwlGreetingCutscene.PlayGreeting()` |
| `OwlGreetingCutscene.cs` | ลำดับ visual novel: cache frame นกฮูก, สลับ frame ตามเวลา, zoom/dim background, เล่นเสียง, focus หมี, reveal หนังสือ, เตรียมและเปิด `MagicStonePuzzleController` |
| `MagicStonePuzzleController.cs` | controller ของ puzzle หินเวท: หา stone/slot, reveal stone ทีละก้อน, เปิดให้ drag, snap stone เข้า slot, แทนที่ stone เดิมถ้าช่องถูกใช้, เมื่อครบสองช่องจะเล่นพิธีกรรมสั่น/flash แล้วโหลด scene ถัดไป |
| `MagicStonePuzzleStone.cs` | component ของหินแต่ละก้อน: implements `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, จำตำแหน่งบ้านและ slot ปัจจุบัน, reveal/move/return animation, ส่ง event drag/release ให้ controller ตัดสินใจ |

## ลำดับ Gameplay จากโค้ด

```mermaid
sequenceDiagram
    participant Player as Player
    participant World as WorldMapIslandIntroFlow
    participant Quest as QuestPointInteractable
    participant Bear as BearCutsceneEntrance
    participant Owl as OwlGreetingCutscene
    participant Puzzle as MagicStonePuzzleController
    participant Stone as MagicStonePuzzleStone

    Player->>World: แตะหรือคลิกเกาะ
    World->>World: ตรวจเกาะ playable / locked / completed
    World->>Quest: โหลด quest_map1
    Player->>Quest: คลิกป้าย / หิน / หมี
    Quest->>Quest: pulse, bounce, zoom, white flash
    Quest->>Bear: โหลด cut_scene1
    Bear->>Owl: reveal แล้ว PlayGreeting()
    Owl->>Puzzle: PrepareForIntro() และ PlayIntroReveal()
    Player->>Stone: drag หิน
    Stone->>Puzzle: HandleDragStarted / HandleStoneReleased
    Puzzle->>Puzzle: snap เข้า slot และตรวจครบ 2 ช่อง
    Puzzle->>Puzzle: ritual shake + white flash
    Puzzle->>Player: โหลด practice.unity
```

## หมายเหตุสำคัญ

- `Assets/Scripts/` คือสคริปต์ gameplay ของโปรเจกต์นี้ ส่วน `TextMesh Pro/Examples & Extras/Scripts` เป็นตัวอย่างจาก package ไม่ใช่ระบบเกมหลัก
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` เป็นโฟลเดอร์ที่ Unity สร้างระหว่างใช้งาน โดยทั่วไปไม่ใช่ source หลักของเกม
- `ProjectSettings/EditorBuildSettings.asset` มี scene `minigame1-1`, `minigame1-2`, `minigame1-3` อยู่ใน Build Settings แต่ตอนสำรวจไฟล์ไม่พบไฟล์ `.unity` เหล่านี้ใน `Assets/Scenes/`
- ชื่อโฟลเดอร์ `visaul_novel` น่าจะตั้งใจหมายถึง `visual_novel` แต่โค้ดและ asset ปัจจุบันอ้างตามชื่อที่มีอยู่จริง
