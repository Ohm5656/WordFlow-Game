# คู่มือสคริปต์ WorldMap → Island2 → reference_forest

เอกสารชุดนี้อ้างอิงโค้ดในโปรเจกต์ ณ วันที่ 21 มิถุนายน 2026 และอธิบายทุกบรรทัดผ่าน “ช่วงเลขบรรทัด” โดยรวมบรรทัดว่าง วงเล็บปีกกา และคำสั่งที่ทำงานเป็นหน่วยเดียวกันไว้ด้วยกัน

## ลำดับที่แนะนำให้อ่าน

1. [ระบบ Fade และการเริ่มทำงานอัตโนมัติ](02_FADE_AND_BOOTSTRAP_TH.md)
2. [ระบบเกาะและการเปลี่ยนซีนจาก WorldMap](01_WORLDMAP_PROBLEM_ISLANDS_TH.md)
3. [ลำดับเหตุการณ์ทั้งหมดใน reference_forest](03_QUEST_PATH_SEQUENCE_TH.md)

## ภาพรวมการไหลของโปรแกรม

```text
เปิด WorldMap
  ↓
WorldMapFadeIn สร้าง Canvas สีดำและค่อย ๆ ลด alpha
  ↓
WorldMapProblemIslands อ่าน PlayerPrefs และแสดง animation ปลดล็อก Island2
  ↓
ผู้เล่นกด Island2
  ↓
SceneFadeController.Cover() เพิ่ม alpha 0 → 1 จนจอดำ
  ↓
SceneManager.LoadScene("reference_forest")
  ↓
SceneFadeController เริ่ม Reveal อัตโนมัติและตั้ง RevealComplete = false
  ↓
ReferenceForestPlayHider ซ่อนวัตถุที่ไม่ควรแสดงตอนเล่น
  ↓
QuestPathSequence รอ RevealComplete
  ↓
จอดำค่อย ๆ จางออก แล้วลำดับเควสต์จึงเริ่ม
```

## แนวคิดหลักที่ระบบนี้ใช้

- `Coroutine` และ `IEnumerator`: กระจายงาน animation หลายเฟรมโดยไม่ค้าง main thread
- `yield return null`: รอหนึ่งเฟรม
- `WaitForSeconds` / `WaitForSecondsRealtime`: รอตามเวลาเกมหรือเวลาจริง
- `RuntimeInitializeOnLoadMethod`: ให้ระบบเริ่มเองโดยไม่ต้องลาก component ลง Scene
- `SceneManager.sceneLoaded`: รับ event ทุกครั้งที่ Unity โหลด Scene
- `PlayerPrefs`: เก็บความคืบหน้าการปลดล็อกข้าม Scene และข้ามการเปิดเกม
- `CanvasGroup.alpha`: ทำ fade เต็มหน้าจอ
- `Time.unscaledDeltaTime`: ทำ fade ต่อได้แม้ `Time.timeScale` เป็น 0
- `Mathf.Sin`, easing และ interpolation: ทำ animation ให้นุ่มแทนการกระโดดค่า
- conditional compilation: รองรับทั้ง Input System ใหม่และ Legacy Input
- defensive null checks: ป้องกัน `NullReferenceException` เมื่อ reference บางตัวไม่ได้ผูกใน Inspector

## ไฟล์ที่ควบคุม flow นี้

| ไฟล์ | หน้าที่ |
|---|---|
| `WorldMapFadeIn.cs` | Fade ดำออกเมื่อเข้า WorldMap |
| `WorldMapProblemIslands.cs` | อ่าน progress, ปลดล็อกเกาะ, รับคลิก และโหลด Scene |
| `Adventure/SceneFadeController.cs` | Fade เข้าดำก่อนออก Scene และ fade ดำออกเมื่อเข้า Scene เป้าหมาย |
| `ReferenceForestPlayHider.cs` | ซ่อนวัตถุบางตัวเฉพาะตอน Play |
| `QuestPathSequence.cs` | ควบคุมการเดินและลำดับเควสต์ทั้งหมดในป่า |

## ค่าที่ Scene ต้องมี

- `Island2` ใน `WorldMap.unity` ต้องมี `SceneName: reference_forest`
- `WorldMap.unity` และ `reference_forest.unity` ต้องอยู่ใน Build Settings และเปิดใช้งาน
- ชื่อ Scene ต้องตรงตัวพิมพ์กับ `"WorldMap"` และ `"reference_forest"`
- ชื่อ GameObject เกาะต้องตรงกับ `Island2`, `Island3`, `Island4`, `Island5`
- ใต้เกาะควรมี `Visuals`, `ClickArea`, `Stars` และวัตถุชื่อขึ้นต้นด้วย `lock` / `unlock`

