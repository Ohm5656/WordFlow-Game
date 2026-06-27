# QuestPathSequence.cs — ลำดับเหตุการณ์ใน reference_forest

ไฟล์: `Assets/Scripts/QuestPathSequence.cs` บรรทัด 1–1040

หน้าที่: เป็น timeline แบบ Coroutine สำหรับการเดินของตัวละคร, การปรากฏ/หายของเควสต์, การบินของอีกา, การเปลี่ยนพืช, กลางวันเป็นกลางคืน และการเข้า encounter Scene

## บรรทัด 1–24: imports, documentation และ class

- `1–4` Coroutine, collection, Unity core และ SceneManager
- `5–7` นำ Input System เฉพาะเมื่อเปิดใช้
- `9–23` XML documentation บอกภาพรวม beat และวิธีขยับตัวละคร
- `24` ประกาศ sealed MonoBehaviour

## บรรทัด 26–47: ตัวละครและ waypoint

- `26–33` reference ตัวละคร, Animator, SpriteRenderer และ auto walker เก่าที่ต้องปิด
- `35–47` waypoint `wp1` ถึง `wp12`

เทคนิค: Scene ถูก author ด้วย Transform waypoint; logic ไม่ต้อง hard-code coordinate

## บรรทัด 49–63: Quest 1 และ Quest 2

- `49–53` Sprite ชาวบ้าน/หมีตามชื่อ field เดิม และ root/sprite เครื่องหมายตกใจ
- `55–62` root/sprite อีกาสองตัวและ marker
- `63` path ใต้ `Resources` สำหรับโหลดทุก frame ของ spritesheet

## บรรทัด 65–84: tuning และ Scene link

- `66` delay หลัง fade
- `67` ความเร็วเดิน
- `68` ระยะถือว่าถึงเป้าหมาย
- `69` ระยะ fade actor
- `70` padding ของ click bounds
- `71–72` เวลาที่ quest แสดงก่อน auto-complete
- `73–74` ค่า Animator speed ระหว่างเดิน
- `75–76` เวลาหันหน้าก่อน quest แรก
- `77–78` vibration
- `80–84` Scene encounter และระยะ fade เข้าดำ

## บรรทัด 86–97: การบินอีกาชุดแรก

- waypoint A/B ของอีกาสองตัว
- ความเร็วบิน, FPS กระพือปีก และการ flip ตามทิศ x

## บรรทัด 99–112: Quest 3 สุนัขจิ้งจอก

- `101–102` ตัวเดินปกติ
- `104–110` ตัวป่วยสองตัวและ marker
- `111–112` fraction 0–1 ที่ใช้วางจุด trigger ระหว่าง wp2 → wp3

`[Range]` จำกัด slider ใน Inspector แต่ runtime ยังใช้ `Clamp01` ซ้ำเพื่อความปลอดภัย

## บรรทัด 114–132: Quest 4 อีกาและแปลงผัก

- root/sprite อีกาสองตัว
- marker หลัก
- array waypoint วงปิด
- ชุดพืชเสียก่อน quest และพืชดีหลัง quest
- marker เสริมหลายตัว

## บรรทัด 134–155: Quest 5, night และ runtime state

- ตัวหมูเดิน, หมูป่วย, marker และ trigger fraction
- `NightLighting` และระยะเปลี่ยนกลางคืน
- flag ควบคุม Coroutine บิน
- `facingLock` ใช้ `-1` หมายถึงไม่ล็อก
- dictionary cache frame อีกาตามชื่อ

## บรรทัด 157–199: Awake

- `159–162` ปิด auto walker ที่ผูกจาก Inspector
- `164–172` ป้องกันซ้ำโดยหา `QuestAutoWalker` บน body แล้วปิด
- `174–178` ซ่อน actor quest 1–2
- `180–183` ซ่อนสุนัขจิ้งจอกป่วย
- `185–189` ซ่อน quest 4 รวม array marker
- `191–194` ซ่อนหมูป่วย
- `195–198` ตั้งแสงกลางวัน

เทคนิค: เตรียม state ใน `Awake` ก่อนเฟรมแรก ลดการกระพริบของวัตถุ

## บรรทัด 201–215: Start และ LateUpdate

- `201–204` เริ่ม timeline
- `206–215` ถ้าล็อกทิศ ให้บังคับ parameter Animator หลัง update อื่น ๆ เพื่อชนะ `CharacterAppearance`

เทคนิค: `LateUpdate` เหมาะกับการ override ค่าที่ script อื่นอาจเขียนใน Update/FixedUpdate

## บรรทัด 217–248: ซ่อน/แสดง actor แรก

- `217–228` ตั้ง alpha 0 ก่อนปิด root
- `230–248` เปิดชาวบ้าน/หมีและ marker พร้อม alpha 1 เมื่อกลับจาก encounter

## บรรทัด 250–314: เริ่ม timeline และ Beat 1

- `255` อ่าน flag static ว่ากลับจาก encounter หรือเข้าใหม่
- `256–269` ถ้ากลับมา ให้วาง body ที่ wp1, หัน wp2 และแสดง actor ก่อน fade เปิด
- `271–273` รอ `SceneFadeController.RevealComplete`; นี่คือ gate ที่ทำให้ gameplay ไม่เดินใต้จอดำ
- `275–278` delay หลัง reveal
- `280–288` เส้นทาง resume: reset flag, hold แล้ว fade actor แรกออก
- `289–305` เส้นทางเข้าใหม่: เดิน wp1, หัน wp2, hold, vibrate และ reveal
- `306` รอให้ผู้เล่นเห็นเหตุการณ์
- `308–313` ตั้ง flag ให้ encounter รู้ว่าต้องกลับป่า, fade เข้าดำ, โหลด encounter และหยุด Coroutine ปัจจุบัน

## บรรทัด 316–342: Beat 2 อีกากีดขวาง

- ปลด facing lock และเดิน wp2
- หันไป wp3
- เปิดอีกาสองตัว, เริ่มบิน และ fade เข้า
- vibrate และ reveal marker
- hold, ปิด patrol flag แล้ว fade ทั้งชุดออก

## บรรทัด 343–386: Beat 3 สุนัขจิ้งจอกป่วย

- `347` หา trigger ด้วย `Vector3.Lerp`
- เดินถึง trigger
- ปิดตัวปกติ เปิดตัวป่วยและ fade เข้า
- เดินต่อถึง wp3
- มี fallback ถ้า wp2 หายแต่ wp3 มี
- หัน/เดิน wp4
- hold, fade ชุดป่วยออก และเปิดตัวเดินปกติกลับ

## บรรทัด 388–435: Beat 4 อีกาบนแปลงผัก

- เดิน wp5 และหัน wp6
- เปิดอีกา, marker หลักและ marker เสริม
- เริ่ม loop flight
- เริ่ม Coroutine fade สองชุดแบบขนานโดยไม่ `yield return`
- เดิน wp6 ระหว่าง fade/flight ยังทำงาน
- hold, หยุด patrol และ fade ออก
- `SwapCrops` สลับพืชเสียเป็นพืชดี

เทคนิค: `StartCoroutine` โดยไม่ yield คือ concurrency แบบ cooperative บน main thread ไม่ใช่ OS thread

## บรรทัด 437–509: Beat 5 และ Beat 6

- หันและเดิน wp6 → จุด trigger → wp7
- ที่ trigger ปิดหมูปกติ เปิดหมูป่วยและ marker
- เดิน/หันต่อ wp8
- hold แล้ว fade หมูป่วยออกและคืนหมูปกติ
- เริ่ม `FadeNight` พร้อมเดิน wp9
- `WalkFacing` ต่อ wp10, wp11, wp12
- fade body ออกเมื่อจบ

## บรรทัด 513–529: WalkFacing

- ไม่มีปลายทางให้ `yield break`
- เลือกจุดอ้างอิงจาก `from` หรือ body
- คำนวณ orientation, hold 0.4 วินาที
- ปลด lock แล้วเดิน

## บรรทัด 531–547: FadeOutBody

- ปลด direction lock
- เลือก `bodySprite` หรือหา SpriteRenderer บน body เป็น fallback
- fade 1 → 0
- ปิด GameObject body

## บรรทัด 549–564: FadeNight

- ไม่มี NightLighting ให้จบ
- loop ตาม `nightFadeDuration`
- ส่งค่า SmootherStep 0 → 1 ให้ระบบแสง
- บังคับ 1 เมื่อจบ ป้องกัน floating-point ไม่ถึงปลาย

## บรรทัด 568–601: MoveTo

- guard body
- รักษา z เดิมเพื่อเดินเฉพาะระนาบ 2D
- loop จนระยะน้อยกว่า `arriveDistance`
- คำนวณ step ไม่ให้เดินเลยเป้าหมายด้วย `Mathf.Min`
- normalize delta ด้วย `delta / dist`
- บังคับ Animator speed เพราะ movement ต่อเฟรมอาจต่ำกว่า threshold ของ controller เดิม
- รอหนึ่งเฟรมทุก iteration
- snap ตำแหน่งสุดท้ายให้ตรงเป๊ะ

## บรรทัด 604–614: OrientationFor

- เปรียบเทียบขนาดแกน x/y เพื่อเลือกแกนเด่น
- คืนรหัสตาม controller เดิม: 0 ขึ้น, 2 ซ้าย, 4 ลง, 6 ขวา

## บรรทัด 617–627: Vibrate

- option guard
- compile `Handheld.Vibrate()` เฉพาะ Android/iOS
- desktop จะเป็น no-op และไม่เกิด compile dependency ที่ไม่จำเป็น

## บรรทัด 631–724: helper reveal/fade

- `631–636` เปิด root สองชุดแล้ว fade พร้อมกัน
- `638–649` ตั้ง alpha 0 ก่อนเปิด root ป้องกันวัตถุ flash
- `651–660` ซ่อน array โดยรองรับ array ยาวไม่เท่ากัน
- `662–671` เปิด array แบบเดียวกัน
- `673–686` fade ออกก่อนปิด root
- `688–699` Coroutine interpolation alpha; ใช้ SmootherStep และบังคับค่าปลาย
- `701–712` overload สำหรับ array
- `714–724` overload สำหรับ SpriteRenderer ตัวเดียว; `Color` เป็น struct จึง copy แก้แล้ว assign คืน

## บรรทัด 726–799: ระบบคลิกรุ่นเดิม

- `728–739` รอจน click โดนหนึ่งใน target
- `741–761` ขยาย Sprite bounds ด้วย padding และตรวจจุด
- `763–799` อ่านเมาส์/สัมผัสและแปลง screen → world

สถานะปัจจุบัน: `RunSequence` ใช้ `questAutoHold` แทนการเรียก `WaitForClick` ดังนั้นชุด method นี้ยัง compile ได้แต่ไม่มี caller ใน timeline ปัจจุบัน เป็น code path สำรอง/legacy

## บรรทัด 803–820: StartCrowFlight

- เปิด patrol flag
- โหลด sprite cache เมื่อยังไม่มี
- เริ่ม Coroutine บินให้อีกาแต่ละตัวที่มี reference

## บรรทัด 823–874: CrowFly แบบ ping-pong

- guard references
- ดึง frame กระพือปีกและคำนวณ interval
- ตั้ง speed ขั้นต่ำและ target แรก
- loop ขณะ patrol flag เป็น true
- รักษา z
- ถ้าถึงเป้าหมายให้ snap และสลับ A/B
- ถ้ายังไม่ถึง ขยับด้วยความเร็วคงที่และ flipX ตามทิศ
- นับเวลาปีก, วน index ด้วย modulo และเปลี่ยน sprite

## บรรทัด 878–902: StartQuest4Flight

- เปิด flag และโหลด cache
- ต้องมี waypoint อย่างน้อยสองจุด
- อีกาตัวแรกเริ่ม index 0
- อีกาตัวสองเริ่มอีกครึ่งวง เพื่อไม่ซ้อนกัน

## บรรทัด 905–964: CrowFlyLoop

- guard
- เตรียม flap/speed
- normalize `startIndex` ให้เป็น index บวกในช่วง array
- loop ขณะ flag true
- ถ้า waypoint null ให้ข้ามไปตัวถัดไป
- ขยับเหมือน CrowFly
- เมื่อถึงมุมให้เพิ่ม index และ modulo กลับต้น array
- animate flap แบบเดียวกัน

## บรรทัด 966–986: สลับชุดพืช

- `SwapCrops` ปิด before และเปิด after
- `SetActiveAll` guard array แล้ววนเฉพาะ element ที่ไม่ null

## บรรทัด 988–1004: LoadCrowSprites

- สร้าง dictionary
- path ว่างให้หยุด
- `Resources.LoadAll<Sprite>` โหลด sub-sprite ทั้งหมด
- เก็บตามชื่อเพื่อค้น frame O(1) โดยประมาณ

## บรรทัด 1007–1033: GetFlapFrames

- guard sprite/cache
- อ่านชื่อ sprite ปัจจุบัน
- หา `_` ตัวสุดท้ายและ parse เลขท้าย
- prefix คือชื่อก่อน index
- `index / 3 * 3` หา frame แรกของแถวที่มีสาม frame
- ลองดึง frame 3 ตัวจาก dictionary
- คืน array ถ้าพบอย่างน้อยหนึ่งตัว ไม่เช่นนั้น null

เทคนิค: integer division ใช้จัด index เข้ากลุ่มละสามโดยไม่ต้องเก็บ metadata เพิ่ม

## บรรทัด 1035–1040: SmootherStep

- clamp 0–1
- polynomial `6t^5 - 15t^4 + 10t^3`
- ได้ความเร็วและความเร่งเป็นศูนย์ทั้งต้นและปลาย จึงนุ่มกว่า linear
- ปิด method และ class

