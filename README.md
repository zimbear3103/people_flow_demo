## 1) Bạn tổ chức code theo cách nào? Vì sao chọn cách tổ chức đó?

### Tổng quan kiến trúc

- Không theo MVC thuần; dùng **state machine + controller trung tâm + singleton managers + event**.
- Mục tiêu: **wire nhanh**, truy cập state/data toàn cục dễ, phù hợp prototype puzzle nhỏ.

### Luồng state cấp app

- **MainStateManager** — máy trạng thái cấp app: `Splash → Loading → MainMenu → Gameplay`. Mỗi state có 3 pha **Enter / Update / Exit**.
- Khi vào **Gameplay**:
    - `UIManager.ShowScreen(...)`
    - `GamePlayController.StartLevel()`
    - Mỗi frame: `GamePlayController.OnUpdate()`

### Controller trung tâm của gameplay

- **GamePlayController** (cũng là một state machine):
    - Giữ điểm, phán **thắng/thua**.
    - Nhận report từ các hệ thống gameplay qua `ReportHoleCompleted` / `ReportRunwayJam` / `ReportTimeOut`.
    - Logic kết quả: đủ hole → `WinPeopleFlow`; kẹt đường hoặc hết giờ → `LosePeopleFlow`.

### Giảm phụ thuộc bằng event

- Dùng C# events: `OnHoleProgress` / `OnLevelWin` / `OnLevelLose`.
- Các hệ thống **Hole / Lane / Factory** đăng ký `OnHoleProgress` để tự mở khóa cơ chế (Frozen/Gate, barrier, ice) theo tiến độ.
- Có thêm event-bus dạng key-string (**EventManager**) — hiện mới dùng cho sự kiện `"lose"`.
- Luồng UI → controller hiện vẫn gọi thẳng qua singleton.

### Data & lưu trữ

- **Level config**: `ScriptableObject (LevelData)` → đưa vào `LevelManager.Build`.
- **Player data**: (Level, Coin) trong `UserProfile`.

### Lý do chọn & nhược điểm

- Lý do: **ship nhanh**, setup nhanh, phù hợp prototype nhỏ.
- Nhược điểm:
    - Singleton + `FindAnyObjectByType` + gọi global → **khó unit-test**, khó decouple.
    - Nếu scale lớn: tách rõ tầng data (Model) và đẩy giao tiếp qua **event-bus / interface** nhiều hơn.

---

## 2) Mở rộng lên 100 level như thế nào?

### Hiện tại đã làm

- Mỗi level là một `ScriptableObject LevelData` → loop, lane, hole/factory, mục tiêu, thời gian… đều là **data thuần**, thêm level **không sửa code** (hiện có 5 asset mẫu `Level_1..5`).
- Lúc chạy, `GamePlayController` giữ mảng `LevelData[] m_levels`, chọn level theo `UserProfile.Level`, rồi `LevelManager.Build(...)` dựng cảnh; tiến độ lưu PlayerPrefs qua `UserProfile` + `SaveManager`.
- Có scene thiết kế **LevelDesign.unity** kèm công cụ `ExportLevelData` để bố trí track/lane/hole trực quan rồi export ra `Level_{n}.asset`.
- `SupplyDealer` sinh hàng đợi nhân vật theo nguyên tắc **cung == cầu** và có seed → level mẫu luôn giải được.

### Có thể làm thêm (production-ready)

1. **Addressables** cho nguồn level động
    - Hiện 5 asset đang kéo cứng vào `m_levels` và nằm trong `Resources_moved`.
    - Kế hoạch: chuyển `LevelData` sang Addressables (label theo nhóm), load async theo khóa, không cần build lại app khi thêm level.
2. **Firebase Remote Config**
    - Chỉnh tham số/độ khó, A/B test live-ops không cần phát hành bản mới.
    - Kết hợp remote catalog để đẩy level mới từ CDN.
3. **Cloud Save**
    - Nâng `SaveManager` từ PlayerPrefs lên cloud để sync tiến độ đa thiết bị.
    - Giữ PlayerPrefs làm fallback offline.
4. **Hoàn thiện pipeline authoring**
    - Import ngược (asset → dựng lại layout để sửa).
    - Batch export.
    - Màn level-select cuộn được.

---

## 3) Nếu người chơi fail quá nhiều — chỉnh ở đâu trước?

> Lưu ý trung thực: trong code hiện chỉ có 2 lý do thua (`LoseReason.RunwayFull`, `LoseReason.TimeOut`) và **chưa được log ở đâu**. Nên muốn quyết định dựa trên dữ liệu thua thực tế, việc đầu tiên là hook vào `OnLevelLose` để log `reason + levelNumber + thời gian chơi` (`m_levelTimeSpent` đã đo sẵn).

### Khi chưa có dữ liệu thua / chưa rõ level

Ưu tiên 2 thứ dễ kiểm soát:

1. **Thứ tự & mật độ khu chờ**
    - Thứ tự do `characters[0]` (ra trước) quyết định.
    - Lane gom màu giống nhau thành nhóm.
    - → sắp lại để màu cần trước thì ra trước, giảm "kẹt màu".
2. **Tiết tấu mở khóa**
    - Barrier / Frozen-Gate / ice factory đều dựa trên `unlockAfterHolesCompleted`.
    - → chỉnh ngưỡng để nội dung khó xuất hiện sớm/trễ hơn.

### Khi đã có dữ liệu thua (đi theo nguyên nhân)

- Thua vì **TimeOut**:
    - Nới `timeLimit`, hoặc tăng `runSpeed`, hoặc giảm tổng nhu cầu (bớt hole / giảm `requiredCount`).
- Thua vì **RunwayFull** (hay gặp nhất):
    - Sức chứa runway tính **tự động theo độ dài vòng và bề rộng group** (không phải `runwayCapacity` trừ khi tắt auto).
    - Điều chỉnh:
        - Nới loop (`loopWidth/Height`).
        - Giảm `groupSize`.
        - Quan trọng nhất: **cân lại cung–cầu** — tổng nhân vật trong các lane (`characters`) phải khớp/dư so với tổng `requiredCount` của hole.

> Lưu ý cho đúng code: hiện chưa có script "vật cản" độc lập — việc chặn/mở đều qua các cờ unlock trên; còn "hang sinh ra hole" chính là `HoleFactory`, nó sản xuất hole lần lượt chứ không phá hole.

---

## 4) Mechanic nào trong prototype này ảnh hưởng retention nhiều nhất? Vì sao?

Theo em: vòng lặp căng thẳng giữa **"fulfill hole"** và **giới hạn sức chứa (capacity) của đường đua**, với input chính là **canh thời điểm tap/hold** để thả từng nhóm.

### Vì sao?

1. **Đây là core loop**
    - Mỗi hole có cơ chế reserve/commit (`CanAccept / TryReserve / Commit`).
    - Minion đúng màu chạy ngang hole sẽ giữ chỗ rồi nhảy vào lấp.
2. **Thua công bằng → tạo động lực chơi lại**
    - Có 2 kiểu thua:
        - (a) Người chơi chủ động thả thêm khi runway không còn chỗ.
        - (b) **Deadlock thật sự** — runway đầy *và* không còn nước đi hợp lệ (`HasViableMove`).
    - Runway đầy nhưng vẫn đang thoát quân → **không** bị tính thua.
    - → Người chơi thấy thua "do mình tính sai" nên muốn thử lại.
3. **Skill expression đến từ timing**
    - Tính trước hole nào sắp hoàn thành để thả đúng màu, đúng lúc.
    - Tránh làm tràn runway.
