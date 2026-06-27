# ระบบ Fade และ Bootstrap อัตโนมัติ

เอกสารนี้ครอบคลุม:

- `Assets/Scripts/WorldMapFadeIn.cs` บรรทัด 1–146
- `Assets/Scripts/Adventure/SceneFadeController.cs` บรรทัด 1–174
- `Assets/Scripts/ReferenceForestPlayHider.cs` บรรทัด 1–97

บรรทัดว่างใช้แบ่งแนวคิด ส่วน `{` และ `}` กำหนดขอบเขต class, property, method, loop และเงื่อนไข

---

## 1. WorldMapFadeIn.cs

หน้าที่: เมื่อเข้า Scene `WorldMap` ให้เริ่มด้วยจอดำ แล้วค่อย ๆ เปิดเผยแผนที่

### บรรทัด 1–12: namespace, class และค่าคงที่

- `1` นำ `IEnumerator` มาใช้สำหรับ Coroutine
- `2` นำ Unity API หลัก เช่น `MonoBehaviour`, `GameObject`, `Time`, `Mathf`
- `3` นำระบบ Scene และ event `sceneLoaded`
- `4` นำ uGUI เช่น `Canvas`, `CanvasGroup`, `Image`
- `5` เว้นบรรทัดแบ่งส่วน
- `6` ประกาศ class แบบ `sealed` หมายถึงไม่ต้องการให้มี class อื่นสืบทอด
- `7` เปิดขอบเขต class
- `8` ชื่อ Scene ที่ script ยอมทำงาน
- `9` ชื่อ GameObject ตัวควบคุม ใช้ตรวจว่าถูกสร้างไปแล้วหรือยัง
- `10` ชื่อ Canvas สีดำ
- `11` ระยะ fade เริ่มต้น 3.2 วินาที
- `12` หน่วงก่อน fade 0.12 วินาที

เทคนิค: ใช้ `const` เพราะค่าเหล่านี้เป็น configuration คงที่ระดับ class และไม่ต้องสร้างซ้ำต่อ instance

### บรรทัด 14–28: สถานะ static และ property IsFadeBlocking

- `14` บอกว่าระบบคาดว่าจะมี fade เริ่มขึ้น
- `15` บอกว่า Coroutine fade กำลังทำงานจริง
- `16` เก็บเวลาจริงโดยประมาณที่ fade ควรเสร็จ
- `18–28` ประกาศ property อ่านอย่างเดียวจากภายนอก
- `20–27` getter รวมเงื่อนไขด้วย OR
- `22` บล็อกเมื่อ fade ถูกจองไว้
- `23` บล็อกเมื่อ fade กำลังวิ่ง
- `24` บล็อกตามเวลาสำรอง
- `25–26` บล็อกถ้ายังพบ GameObject setup หรือ Canvas อยู่

เทคนิค: นี่เป็น “หลายชั้นป้องกัน” เพื่อไม่ให้ animation ปลดล็อกเกาะเริ่มก่อน fade จบ แม้ lifecycle ของ Unity จะคลาดกันหนึ่งหรือสองเฟรม

ข้อสังเกต: `GameObject.Find` ไม่ควรเรียกหนักทุกเฟรม แต่ property นี้ถูกใช้เฉพาะช่วงรอเริ่ม Scene จึงยอมรับได้

### บรรทัด 30–34: ค่าปรับจาก Inspector

- `30–31` เปิดให้แก้ระยะ fade ใน Inspector แต่ยังเป็น `private`
- `33–34` เปิดให้แก้เวลาหน่วง

เทคนิค: `[SerializeField] private` รักษา encapsulation แต่ยัง author ค่าใน Unity Editor ได้

### บรรทัด 36–47: Bootstrap และ sceneLoaded event

- `36` สั่ง Unity เรียก method หลังโหลด Scene โดยไม่ต้องติด component ไว้ล่วงหน้า
- `37–42` method เริ่มระบบ
- `39` ถอด callback เดิม ป้องกันสมัคร event ซ้ำหลัง domain reload
- `40` สมัคร callback ใหม่
- `41` ตรวจ Scene ปัจจุบันทันที เพราะ event อาจผ่านไปแล้ว
- `44–47` callback เมื่อ Scene ใหม่โหลดเสร็จ และส่งต่อไป method กลาง

เทคนิค: รูปแบบ `-=` ก่อน `+=` เป็น idempotent subscription ทำซ้ำกี่ครั้งก็เหลือ callback ชุดเดียว

### บรรทัด 49–70: ตัดสินใจว่าจะเริ่ม fade หรือไม่

- `51` อ่าน active Scene
- `52–58` ถ้าไม่ใช่ WorldMap ให้ reset static state แล้วออก
- `60–63` ถ้ามี setup object อยู่แล้ว แปลว่าเริ่มไปแล้ว จึงไม่สร้างซ้ำ
- `65` ตั้งสถานะว่ากำลังจะ fade
- `66` คำนวณเวลาจบโดยประมาณด้วย realtime
- `68` สร้าง host GameObject
- `69` เพิ่ม component นี้แบบ runtime; Unity จะเรียก `Start`

เทคนิค: เป็น self-bootstrap component ไม่มี dependency กับ Scene wiring

### บรรทัด 72–109: Coroutine FadeInRoutine

- `72–75` `Start` เริ่ม Coroutine
- `77–81` ตั้ง flag และคำนวณ deadline จากค่าจริงใน Inspector
- `83` สร้าง Canvas สีดำ
- `84` alpha 1 คือดำทึบ
- `85` บล็อก raycast ระหว่างยังมองไม่เห็น Scene
- `87` รอหนึ่งเฟรม ให้ Canvas ถูก render ก่อน
- `89–92` รอ delay ด้วยเวลาจริง ไม่ได้รับผลจาก time scale
- `94` บังคับ duration ขั้นต่ำ ป้องกันหารศูนย์
- `95` loop ตามเวลาจริง
- `97` แปลงเวลาเป็นค่า 0–1
- `98` easing แล้วกลับทิศด้วย `1 - ค่า` จึงได้ดำ 1 → ใส 0
- `99` คืนการควบคุมให้ Unity หนึ่งเฟรม
- `102–106` บังคับค่าสุดท้ายและ reset state
- `107–108` ทำลาย Canvas และ host เพื่อไม่ทิ้ง object ค้าง

เทคนิค: ใช้ `Time.unscaledDeltaTime` เพื่อให้ transition ไม่ค้างเมื่อเกม pause

### บรรทัด 111–139: สร้าง Canvas เต็มหน้าจอ

- `113` สร้าง GameObject สำหรับ Canvas
- `114–118` เพิ่ม `RectTransform` และ stretch เต็ม parent/screen ด้วย anchor 0–1 และ offset 0
- `120–122` เพิ่ม Canvas แบบ overlay และตั้ง sorting order สูงมาก
- `124` เพิ่ม CanvasGroup เพื่อควบคุม alpha ทั้งชุดด้วยค่าจุดเดียว
- `126` สร้างลูกชื่อ Fade Panel
- `127` parent โดย `worldPositionStays = false`
- `128–132` stretch panel เต็ม Canvas
- `134` เพิ่ม Image
- `135` ตั้งสีดำ
- `136` ปิด raycast ของ Image เพราะ CanvasGroup เป็นผู้ควบคุมการบล็อก
- `138` คืน CanvasGroup ให้ Coroutine

### บรรทัด 141–146: easing และปิด class

- `141–145` ฟังก์ชัน Ease In-Out แบบ sine
- `143` จำกัด input ให้อยู่ 0–1
- `144` สูตรเริ่มช้า เร็วกลางทาง และช้าก่อนจบ
- `146` ปิด class

---

## 2. SceneFadeController.cs

หน้าที่: เป็น fade กลางที่ใช้ได้หลาย Scene โดยมีสองทิศ:

- `Cover`: ใส → ดำ ก่อนโหลด Scene
- `Reveal`: ดำ → ใส หลังเข้า Scene

### บรรทัด 1–20: imports, XML documentation และ class

- `1–4` ใช้ Coroutine, Unity core, SceneManager และ UI
- `6–19` XML documentation อธิบาย contract ของ class; IDE สามารถแสดง tooltip จากข้อความนี้
- `20` class เป็น `sealed MonoBehaviour`

### บรรทัด 22–39: Scene ที่เปิด reveal และสถานะ

- `22` constant ชื่อป่า
- `24–25` comment อธิบาย array
- `26–30` รายชื่อ Scene ที่เข้าแล้วต้อง fade ดำออกอัตโนมัติ
- `28` reference_forest
- `29` encounter scene
- `32–34` ชื่อ host และ Canvas
- `36` ระยะ reveal 1.6 วินาที
- `38–39` property static อ่านได้จากทุก script แต่เขียนได้เฉพาะ class นี้; ค่าเริ่ม `true` ป้องกันผู้รอค้างถ้าไม่มี fade

### บรรทัด 41–52: Bootstrap

- `41` เรียกหลังโหลด Scene
- `42–47` สมัคร event แบบไม่ซ้ำและตรวจ Scene ทันที
- `49–52` callback ทุกครั้งที่ Scene โหลด

### บรรทัด 54–73: เริ่ม Reveal

- `56–59` ถ้า Scene ไม่อยู่ใน allowlist ให้หยุด
- `61–64` ป้องกันสร้าง reveal ซ้ำ
- `66–68` ปิด gate ทันที ก่อน `Start` ของ gameplay
- `70` สร้าง host
- `71` เพิ่ม component
- `72` เริ่ม Coroutine reveal

เทคนิคสำคัญ: Unity เรียก `sceneLoaded` ก่อน `Start` ของ MonoBehaviour ใน Scene ดังนั้น `RevealComplete = false` ถูกตั้งทันก่อน `QuestPathSequence.Start`

### บรรทัด 75–84: API Cover

- `75–78` XML documentation
- `79` method static คืน `IEnumerator` จึงใช้ `yield return SceneFadeController.Cover(...)` ได้
- `81–82` สร้าง host/component แบบ runtime
- `83` รอ Coroutine ภายในจนดำเต็มจอ

### บรรทัด 86–102: CoverRoutine

- `88` สร้าง overlay
- `89` เริ่มใส
- `90` บล็อก input
- `92` duration ขั้นต่ำ
- `93–97` เพิ่ม alpha 0 → 1 ด้วย unscaled time และ easing
- `99` บังคับดำเต็ม
- `100–101` ตั้งใจไม่ทำลาย overlay เพราะ `LoadScene` ที่ตามมาจะทำลาย object ใน Scene เดิม และ Scene ใหม่เริ่มด้วยดำเต็มเช่นกัน

เทคนิค: การปล่อย overlay ค้างหนึ่งช่วงสั้น ๆ ป้องกัน “white/scene flash” ระหว่างเปลี่ยน Scene

### บรรทัด 104–124: RevealRoutine

- `106–108` สร้าง overlay ดำเต็มและบล็อก input
- `110` รอ render หนึ่งเฟรม
- `112–117` ลด alpha 1 → 0
- `119–120` บังคับใสและคืน input
- `121` เปิด gate ให้ gameplay เริ่ม
- `122–123` ล้าง overlay และ host

### บรรทัด 126–154: CreateOverlay

ทำงานเหมือน `WorldMapFadeIn.CreateFadeCanvas`:

- `128–133` สร้าง Canvas root และ stretch
- `135–137` Screen Space Overlay, sorting order 10000
- `139` CanvasGroup สำหรับ alpha/input
- `141–147` สร้าง panel ลูกเต็มจอ
- `149–151` Image สีดำและไม่รับ raycast เอง
- `153` คืน CanvasGroup

### บรรทัด 156–167: ShouldSelfReveal

- `158` วนทุกชื่อ Scene ใน array
- `160–163` ถ้าชื่อตรง คืน `true` ทันที
- `166` วนครบแล้วยังไม่ตรง คืน `false`

เทคนิค: เป็น allowlist ที่เพิ่ม Scene ใหม่ได้ง่ายกว่าการเขียน OR ยาว ๆ

### บรรทัด 169–174: EaseInOutSine

- `171` clamp
- `172` สูตร easing sine
- `174` ปิด class

---

## 3. ReferenceForestPlayHider.cs

หน้าที่: ซ่อนวัตถุบางตัวเฉพาะตอนรันเกม แต่ยังให้เห็นใน Editor เพื่อจัดฉากได้

### บรรทัด 1–14: imports, documentation และ class

- `1` Unity core
- `2` Scene API
- `4–11` XML documentation
- `12` `static class`: ไม่มี instance และติดเป็น component ไม่ได้
- `14` ชื่อ Scene เป้าหมาย

### บรรทัด 16–21: รายชื่อวัตถุ

- `16` array อ่านอย่างเดียวในระดับ reference
- `18–20` ชื่อ GameObject ที่ต้องตรงแบบ exact

เทคนิค: แยกชื่อไว้เป็นข้อมูล ทำให้เพิ่ม/ลดรายการได้โดยไม่แก้ algorithm

### บรรทัด 23–34: Bootstrap

- `23` เรียกหลังโหลด Scene
- `24–29` สมัคร event แบบ idempotent และทำงานกับ Scene ปัจจุบัน
- `31–34` callback เมื่อ Scene เปลี่ยน

### บรรทัด 36–68: ซ่อนวัตถุ

- `38` อ่าน active Scene
- `39–42` ไม่ใช่ reference_forest ก็ไม่ทำอะไร
- `44` ดึง root GameObject ทั้งหมด
- `45` วนชื่อทุกตัวในรายการ
- `47` ค้นแบบ exact รวม object inactive
- `48–52` หาไม่เจอให้ warning แล้วข้ามตัวนั้น ไม่ทำให้ทั้งระบบล้ม
- `54–55` comment อธิบายปัญหาว่า `QuestProximityReveal` อาจเปิด object กลับ
- `56` หา component ดังกล่าวจาก parent รวม inactive parent
- `57–61` ถ้ามี controller ให้แจ้ง controller ว่าวัตถุนี้ต้องถูก exclude ถาวร
- `62–66` ถ้าไม่มี controller จึงปิด GameObject ตรง ๆ

เทคนิค: ไม่ได้แค่ `SetActive(false)` อย่างเดียว แต่จัดการ “เจ้าของ state” ของวัตถุ เพื่อไม่ให้ระบบอื่นเปิดกลับภายหลัง

### บรรทัด 70–97: FindByExactName

- `70` comment ระบุว่าเป็น depth-first-like traversal ผ่าน hierarchy ที่ Unity คืนให้
- `71` รับ root array และชื่อเป้าหมาย
- `73–76` guard เมื่อ array เป็น null
- `78` วนแต่ละ root
- `80–83` ข้าม root null
- `85` ดึง Transform ลูกทั้งหมดรวม inactive
- `86–92` วนและเปรียบเทียบชื่อด้วย `==`
- `90` เจอตัวแรกแล้วคืนทันที
- `95` ไม่พบคืน null
- `96–97` ปิด method และ class

ข้อควรระวัง: ถ้ามี GameObject ชื่อซ้ำ จะได้ตัวแรกเท่านั้น ถ้าต้องการระบุแน่นอนควรใช้ hierarchy path หรือ serialized reference

