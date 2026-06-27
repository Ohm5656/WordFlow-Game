# WorldMapProblemIslands.cs — อธิบายทุกช่วงบรรทัด

ไฟล์: `Assets/Scripts/WorldMapProblemIslands.cs` บรรทัด 1–947

หน้าที่รวม: อ่าน progress, แสดงสถานะล็อก, เล่น animation ปลดล็อก, เลือกเกาะที่เล่นได้, รับเมาส์/สัมผัส, fade จอดำ และโหลด Scene

## บรรทัด 1–12: imports และ class declaration

- `1` ใช้ `IEnumerator` สำหรับ Coroutine
- `2` ใช้ collection เช่น `List` และ `HashSet`
- `3` Unity core
- `4` SceneManager
- `6–9` compile เฉพาะเมื่อเปิด Input System ใหม่; นำ Mouse, Touchscreen และ TouchControl มาใช้
- `11` ป้องกันติด component นี้ซ้ำบน GameObject เดียว
- `12` class ปิดการสืบทอดและเป็น MonoBehaviour

## บรรทัด 14–30: progress keys และข้อมูล Scene

- `14–15` key สำหรับ `PlayerPrefs`: ค่าสูงสุดที่ปลดล็อกแล้ว และเกาะที่ต้องเล่น animation ปลดล็อก
- `17` หัวข้อ Inspector
- `18` กล้องที่แปลง screen coordinate เป็น world coordinate
- `19–25` array กำหนด mapping เกาะ → region → Scene
- `21` จุดเชื่อมสำคัญ: `Island2`, region 1, โหลด `reference_forest`
- `22–24` เกาะถัดไปยังไม่มี Scene
- `26` region แรกที่ต้องเปิด
- `27` ให้ animation ปลดล็อกแรกเล่นซ้ำระหว่างยังไม่มี region ถัดไป
- `28` เวลาหน่วงหลังกด
- `29–30` tooltip และระยะ fade เข้าดำ

## บรรทัด 32–63: ค่าปรับ animation และ interaction

- `32–40` ชื่อ prefix ของ lock/unlock, เปิด animation, ความสูง/ระยะเวลา bob, pulse scale และพฤติกรรม click/stars ของเกาะล็อก
- `42–51` การรอ fade ตอนเข้า WorldMap และค่าทั้งหมดของ animation เปลี่ยน lock → unlock
- `53–63` animation หายใจของเกาะที่เล่นได้, glow แบบเลือกเปิด, สี/scale/alpha และ padding ของพื้นที่กด

เทคนิค: `[SerializeField] private` ทำให้ designer ปรับได้โดยไม่เปิด public API

## บรรทัด 65–80: runtime state

- `65` list ของ lock ที่ต้อง bob
- `66` cache ข้อมูลเกาะที่ค้นเจอใน Scene
- `67` set ของ lock ที่กำลังเล่น unlock animation เพื่อไม่ให้ bob ซ้อน
- `68` เกาะที่กดได้ปัจจุบัน
- `69–76` reference และ transform เดิมสำหรับ animation prompt/glow
- `77` อนุญาตรับคลิกหรือยัง
- `78` กันกดซ้ำระหว่างโหลด
- `79` กัน `Update` ทำงานก่อน setup เสร็จ
- `80` cache Sprite glow ที่สร้างด้วยโค้ด

เทคนิค: `HashSet.Contains` เหมาะกับการถามสมาชิกซ้ำ ๆ และเร็วกว่าไล่ list

## บรรทัด 82–106: data classes ภายใน

- `82–88` `IslandProgress` serialize ได้ จึงแสดงเป็น element ใน Inspector
- `85–87` เก็บชื่อ object, ลำดับ region และ Scene
- `90–98` `IslandRuntime` เก็บ reference ที่ค้นจาก Scene และสถานะจริง
- `100–106` `AnimatedTarget` เก็บ transform เดิมและ phase ของ bob

## บรรทัด 108–127: MarkRegionCompleted

- `108` API static ให้ Scene อื่นแจ้งว่า region จบแล้ว
- `110–113` ไม่รับ region ต่ำกว่า 1
- `115` คำนวณ region ถัดไป
- `116` อ่านค่าสูงสุดเดิม
- `117–122` ถ้าปลดล็อกไปแล้ว ไม่ลดค่าเดิม แต่ตั้ง pending เพื่อ replay animation
- `124–126` ถ้าเป็น progress ใหม่ บันทึกทั้ง highest และ pending แล้ว Save

เทคนิค: แยก “ข้อมูลปลดล็อกจริง” ออกจาก “animation ที่ยังต้องเล่น”

## บรรทัด 129–156: Unity lifecycle

- `129–135` `Awake` ใช้ `Camera.main` เป็น fallback
- `137–144` `Start` เป็น Coroutine; รอหนึ่งเฟรม แล้ว ensure progress, cache hierarchy, apply state และเปิด Update
- `146–156` `Update` ทำ animation และตรวจ input เฉพาะหลัง setup

## บรรทัด 158–178: EnsureInitialProgress

- `160` บังคับ region แรกอย่างน้อย 1
- `161` อ่าน progress
- `162–173` ถ้ามี progress แล้ว อาจตั้ง pending เพื่อ replay animation แรก จากนั้นออก
- `175–177` ผู้เล่นใหม่ได้รับ region แรกและ pending unlock ทันที

## บรรทัด 180–214: CacheIslandState

- `182` ล้าง cache เก่า
- `184–187` guard array null
- `189–213` วน config
- `192–195` ข้าม element ที่ไม่มีชื่อ
- `197–201` หา GameObject ตามชื่อและข้ามถ้าไม่พบ
- `203–204` เก็บ root และลูก `Visuals`
- `205–212` สร้าง runtime record; ถ้าไม่มี Visuals ใช้ root และค้น lock/unlock ด้วย prefix

## บรรทัด 216–272: ApplyProgressState

- `218–221` reset animation/cache selection
- `223–228` อ่าน highest/pending และแก้ pending ที่ไม่สมเหตุผล
- `230` ตัวแปรจำเกาะที่ต้องเล่น unlock
- `232–256` วนทุกเกาะ
- `235–236` ตัดสิน unlocked จากเลข region
- `238–240` pending ยังแสดงเป็นล็อกเพื่อให้ animation มีจุดเริ่ม
- `242–245` ลงทะเบียน lock animation
- `247–250` จำ pending island
- `252–255` เลือก region สูงสุดเป็น playable
- `258–261` fallback หากไม่พบเลขตรง
- `263–266` ถ้ามี pending เริ่ม Coroutine unlock
- `267–271` ถ้าไม่มี animation ค้าง เตรียม prompt และเปิดคลิกทันที

## บรรทัด 274–288: FindHighestUnlockedIsland

- วน runtime list และเก็บตัวล่าสุดที่ region ไม่เกิน highest
- คืน fallback ที่ปลดล็อกสูงสุดตามลำดับ array

ข้อสังเกต: ผลลัพธ์พึ่งลำดับ `islands`; ควรเรียง region จากน้อยไปมาก

## บรรทัด 290–312: ApplyIslandLockVisualState

- `292–295` guard null
- `297–301` เปิด/ปิด lock และ reset alpha
- `303–307` ซ่อน unlock icon และ reset alpha
- `309` กดได้เมื่อ unlocked และไม่ได้กำลังแสดงล็อก
- `310` ควบคุม ClickArea
- `311` ควบคุม Stars

## บรรทัด 314–323: AddLockAnimationTarget

- สร้าง record เก็บตำแหน่ง/scale เริ่มต้น
- phase ต่างกันตามจำนวน list ทำให้ lock แต่ละตัว bob ไม่พร้อมกันทั้งหมด

## บรรทัด 325–337: รอ WorldMap fade

- `327` รอหนึ่งเฟรม
- `329` ทำเฉพาะเมื่อเปิด option
- `331` สร้างเวลาสำรอง
- `333–336` รอทั้ง fade blocker และ fallback time

หมายเหตุ: เงื่อนไข OR หมายความว่าจะรออย่างน้อยถึง fallback time แม้ fade จบก่อน

## บรรทัด 339–364: เตรียม unlock animation

- `339–342` delay
- `344` ปิด prompt
- `345–348` กัน bob animation เข้ามาแตะ lock ตัวนี้
- `350–357` เก็บ transform เดิมทั้ง lock และ unlock พร้อม fallback เมื่อ null
- `358–362` ถ้า parent เดียวกัน ใช้ตำแหน่ง lock เป็นจุด transition
- `363–364` เลือก rotation และตำแหน่งแสดง

เทคนิค: ternary operator ทำ null-safe fallback แบบกระชับ

## บรรทัด 366–393: ตั้งค่าเริ่มของภาพ lock/unlock

- `366–373` เปิด lock และ reset transform/alpha
- `375–382` reset unlock ก่อน
- `384–393` เปิด unlock ที่ตำแหน่ง transition ด้วย alpha 0 แล้วจัดศูนย์ภาพให้ตรง lock

## บรรทัด 395–410: ช่วงเขย่า lock

- `395–396` duration ขั้นต่ำและแบ่ง 16% แรกเป็น shake
- `397` loop ต่อเฟรม
- `399` normalized time
- `402` sine หลายรอบและลดแรงลงด้วย `(1 - t)`
- `403` pop เล็กน้อย
- `404–406` apply position, rotation และ scale
- `409` รอเฟรมถัดไป

## บรรทัด 412–439: cross-fade lock → unlock

- `412` เวลาที่เหลือ
- `413–439` loop
- `415–418` easing และ soft pop ร่วมกัน
- `420–426` lock ค่อย ๆ alpha 1 → 0
- `428–436` unlock ค่อย ๆ alpha 0 → 1 และจัด visual center ทุกเฟรม

เทคนิค: cross-fade สอง SpriteRenderer พร้อมใช้ scale เดียวกัน ทำให้ดูเหมือนวัตถุเดียวเปลี่ยนรูป

## บรรทัด 441–483: จบ animation และ fade unlock icon ออก

- `441–449` reset และซ่อน lock
- `451–458` บังคับ unlock แสดงสมบูรณ์
- `460–463` hold
- `465–476` ลด alpha unlock พร้อมย่อ 1 → 0.88
- `478–482` reset transform/alpha แล้วซ่อน เพื่อพร้อมใช้รอบหน้า

## บรรทัด 485–502: เปิดเกาะให้เล่น

- `485–486` เปิด ClickArea และ Stars
- `488–493` ลบ pending key ถ้าตรงกับเกาะนี้
- `495` ลบ lock ออกจาก animation list รวม reference ที่หายไป
- `497–501` ถ้าเป็น playable island ให้ cache prompt และเปิดรับคลิก

## บรรทัด 504–513: IsSceneFadeBlocking

- ถ้า `WorldMapFadeIn` รายงานว่ายังบล็อก คืน true
- ไม่เช่นนั้นตรวจ GameObject Canvas ตามชื่อเป็น fallback

## บรรทัด 515–541: CachePlayableIslandPrompt

- `517–522` ทำลาย glow เก่า
- `524–528` เก็บ transform ฐาน
- `530–535` คำนวณจุดศูนย์จริงของภาพใน coordinate ของ parent เพื่อแก้ pivot ระหว่าง scale
- `537–540` สร้าง glow ถ้าเปิด option

## บรรทัด 543–565: AnimateLocks

- `545–548` option guard
- `550–551` ป้องกัน duration/scale ผิด
- `552–564` วน target
- `555–558` ข้าม null และตัวที่กำลัง unlock
- `560` sine wave พร้อม phase
- `561` map wave -1..1 เป็น 0..1 แล้ว Lerp scale
- `562–563` apply bob และ pulse

## บรรทัด 567–584: AnimatePlayableIslandPrompt

- `569–572` ทำงานเฉพาะเมื่อพร้อมและไม่ได้โหลด
- `574–577` สร้าง sine wave, normalize และคำนวณ breath scale
- `579` ชดเชย pivot เพื่อให้เกาะขยายรอบ visual center
- `580–581` apply scale/rotation
- `583` sync glow

## บรรทัด 586–605: รับคลิกเกาะ

- `588–591` guard หลายสถานะ
- `593–596` อ่าน press
- `598` แปลง screen → world โดยกำหนดระยะ z ตามกล้อง 2D
- `599–602` ตรวจ bounds
- `604` เริ่มโหลด Scene

## บรรทัด 607–649: LoadNextSceneRoutine

- `609–610` ล็อกไม่ให้กดซ้ำ
- `612–617` reset animation ของเกาะ
- `619–622` ซ่อน glow
- `624–627` หน่วงเล็กน้อย
- `629–631` อ่านชื่อ Scene แบบ null-safe
- `633–638` ถ้ามีชื่อ: fade เข้าดำจนเสร็จ แล้ว `LoadScene`
- `639–648` ถ้าไม่มีชื่อ: warning, คืนสถานะ interaction และ glow

นี่คือจุดเชื่อม WorldMap → reference_forest โดยตรง

## บรรทัด 651–665: ตรวจพื้นที่กด

- guard playable island
- รวม bounds ของ Sprite
- ขยาย bounds ด้วย padding
- ตรวจ `Bounds.Contains` โดยตรึง z ให้เท่าศูนย์กลาง bounds

## บรรทัด 667–695: สร้าง glow

- `669–672` ต้องมี bounds
- `674–676` สร้าง object ใต้ island และวางกลาง bounds
- `678–681` เพิ่ม SpriteRenderer, ใช้ sprite ที่ generate และกำหนดสี
- `683–688` วาง sorting layer เดียวกับเกาะแต่ต่ำกว่าหนึ่งลำดับ
- `690–694` คำนวณ scale ให้สัมพันธ์กับขนาดเกาะ

## บรรทัด 697–708: Animate glow

- guard references
- Lerp scale และ alpha สวนกัน
- apply scale และสีใหม่

## บรรทัด 710–744: รวม Sprite bounds

- `712–716` initialize และ guard
- `718` ดึง SpriteRenderer รวม inactive
- `721–741` วน renderer
- `724–730` ข้าม null, ไม่มี sprite, lock/unlock และ glow
- `732–736` renderer แรกตั้ง bounds
- `737–740` ตัวถัดไปใช้ `Encapsulate`
- `743` คืนว่าพบ renderer หรือไม่

## บรรทัด 746–774: หา renderer อ้างอิง sorting

- โครงคล้าย method ก่อนหน้า
- เลือก renderer ที่ `sortingOrder` ต่ำที่สุด เพื่อนำ glow ไปวางด้านหลัง

## บรรทัด 776–808: รองรับ input สองระบบ

- `778–796` compile เมื่อมี Input System ใหม่
- `779–783` เมาส์
- `785–795` วนทุก touch และรับ touch ที่เพิ่งกด
- `798–804` Legacy Input Manager
- `806–807` ไม่มี press ให้คืน false

เทคนิค: preprocessor directive ทำให้ source เดียว build ได้หลาย configuration โดยไม่อ้าง type ที่ package ไม่มี

## บรรทัด 810–828: FindChildByPrefix

- guard root/prefix
- ดึง Transform ลูกทั้งหมดรวม inactive
- ข้าม root เอง
- คืนลูกตัวแรกที่ชื่อขึ้นต้นด้วย prefix

## บรรทัด 830–844: helper ตรวจ renderer พิเศษ

- `830–839` ตรวจชื่อขึ้นต้น lock หรือ unlock
- `841–844` ตรวจว่า target เป็นลูกของ glow หรือไม่

## บรรทัด 846–862: AlignVisualCenter

- guard references และ SpriteRenderer
- คำนวณ delta ระหว่าง `bounds.center`
- เลื่อน target ใน world space ให้ศูนย์ภาพตรงกัน

เทคนิค: ใช้ visual bounds แทน pivot เพราะ sprite lock/unlock อาจมี transparent padding หรือ pivot ต่างกัน

## บรรทัด 864–887: helper เปลี่ยน active และ alpha

- `864–871` หา direct child แล้วเปลี่ยน active เฉพาะเมื่อค่าต่าง ป้องกัน lifecycle callback ที่ไม่จำเป็น
- `873–887` วน SpriteRenderer ลูกทั้งหมด, copy struct `Color`, แก้ alpha แล้ว assign คืน

## บรรทัด 889–932: สร้าง glow ring ด้วยโค้ด

- `891–894` คืน cache ถ้ามีแล้ว
- `896–902` ตั้งขนาด texture และพารามิเตอร์วงแหวน
- `904–914` วนทุก pixel
- `908` ระยะจาก center
- `909` ระยะห่างจากรัศมีวง
- `910–911` สร้าง alpha แบบ feather และยกกำลังสองให้ falloff นุ่ม
- `912` เขียน pixel สีขาวพร้อม alpha
- `916–920` ส่ง pixel เข้า texture, ทำ immutable/read-only หลัง apply, ตั้งชื่อ/wrap/filter
- `922–928` สร้าง Sprite เต็ม texture, pivot กลาง และ pixels-per-unit
- `929–931` ตั้งชื่อและคืน cache

เทคนิค: procedural texture ลดการพึ่ง asset ภายนอก แต่มีค่า allocation ครั้งแรก

## บรรทัด 934–947: easing helpers และปิด class

- `934–938` sine ease in-out ที่ถูกใช้จริง
- `940–946` back easing ซึ่งสร้าง overshoot; ปัจจุบันไม่มีจุดเรียกในไฟล์ จึงเป็น helper ที่เหลือจากการทดลองหรือเตรียมไว้
- `947` ปิด class

