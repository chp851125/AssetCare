# AssetCare 開發慣例

需求以 [REQUIREMENTS.md](REQUIREMENTS.md) 為準。本檔記錄命名、結構與規則；每階段結束時更新。

## 專案基本
- Solution `AssetCare.slnx`，單一專案 `AssetCare`（`AssetCare/AssetCare.csproj`），目標 `net10.0`，不升降級。
  - 需求原文寫專案名為 `AssetCare.Web`；實際建立的是 `AssetCare`，已確認**沿用 `AssetCare`**。根命名空間 `AssetCare.*`。
- 套件：QRCoder、ClosedXML、Serilog.AspNetCore。不得加入付費套件、資料庫、EF Core、測試專案、CI 設定。
- UI 繁體中文（`zh-TW` culture）。

## 資料夾與分層
| 路徑 | 命名空間 | 內容 |
|---|---|---|
| `Domain/Entities`、`Enums`、`Exceptions`、`Rules` | `AssetCare.Domain.*` | 實體、列舉（集中於 `Enums.cs`）、例外、無相依的商業規則 |
| `Application/Services`、`Interfaces`、`Dtos` | `AssetCare.Application.*` | 商業邏輯；`AuditActions`、`SettingKeys` 與共用輔助（`CurrentUserExtensions`、`SettingExtensions`、`InputText`、`EnumNames`）放 `Application/` 根目錄 |
| `Infrastructure/Json`（含 `Seeding`）、`Excel`、`Files`、`Qr`、`Background`、`Email` | `AssetCare.Infrastructure.*` | JSON 資料層、種子工具；ClosedXML、附件實體檔、QRCoder 的轉接；背景服務與 Email（階段 3） |
| `Web/Controllers`、`Views`、`ViewModels`、`ViewComponents`、`Infrastructure` | `AssetCare.Web.*` | 展示層；`Web/Infrastructure` 放驗證、過濾器、擴充方法 |
| `SeedData/` | — | 範例資料（進 Git） |
| `App_Data/`、`logs/`、`outbox/` | — | 執行期資料（不進 Git） |

相依方向：Controller / ViewComponent → Service 介面 → `IRepository<T>`。Application 與 Domain **不得出現 JSON 或 Infrastructure 型別**。
第三方套件（ClosedXML、QRCoder）與檔案系統一律經由 Application 介面（`ISpreadsheetService`、`IQrCodeGenerator`、`IFileStorage`）。

Razor 檢視位置已改為 `/Web/Views/{controller}/{view}.cshtml` 與 `/Web/Views/Shared/`（設定於 `Program.cs`）。

## 命名
- 實體皆繼承 `BaseEntity`（`Id`、`Version`、`CreatedAt`、`UpdatedAt`）。
- Service `XxxService` / 介面 `IXxxService`（一介面一檔），於 `Infrastructure/DependencyInjection.cs` 的 `AddApplication` 註冊為 Scoped。
- Controller `XxxController`；ViewModel `XxxViewModel`；Service 回傳資料用 `Application/Dtos` 的 record。
- JSON 檔名為實體名複數（`JsonFileNames.For`），欄位 camelCase，列舉存字串。
- 列舉中文名稱寫在 `[Display(Name=…)]`，畫面用 `.Label()`。

## 時間
- 時間點（`DateTime`，欄位名 `XxxAt`）一律 UTC；只在顯示時用 `Clock.Format(utc)` 轉台北時間。
- 日曆日期（購入日、保固到期日、保養日、外送日等，欄位名 `XxxDate`）用 `DateOnly`，代表台北日期，與 `ITimeProvider.Today` 比較。
- 只能透過 `ITimeProvider` 取得現在時間；禁止 `DateTime.Now/UtcNow/Today`（唯一例外：`SystemTimeProvider`）。
- 表單輸入的本地時間用 `ITimeProvider.ToUtc` 轉換後儲存。

## 資料層
- `IRepository<T>`：`GetAll`、`GetById`、`Query(Expression)`、`Add`、`Update`、`Delete(entity)`。同步介面。
- 回傳值是**複本**；修改後必須呼叫 `Update`。
- `Version`、`CreatedAt`、`UpdatedAt` 由 Repository 維護。`Update`/`Delete` 版本不符丟 `ConcurrencyException`。
- 編輯表單必須帶 `Version` 隱藏欄位，Service 以表單版本覆寫實體版本後再 `Update`。
- `JsonFileRepository<T>` 為 Singleton：啟動載入、寫暫存檔再取代、執行緒安全。
- 跨多個實體的異動沒有交易；需要一致性的流程（配號、唯一性檢查、扣庫存）須用 `IBusinessLock.Run`，在鎖內「先檢查全部條件、再寫入」。可能發生版本衝突的 `Update` 要排在其他寫入之前。
- 首次啟動由 `SeedData/` 複製到 `App_Data/`；之後只補缺少的檔案。上傳實體檔放 `App_Data/uploads`（`JsonDataStore.UploadDirectory`）。
- 新增實體欄位時：更新實體、產生器、驗證器，重新產生 SeedData。

## 例外與訊息
- `BusinessRuleException`：違反規則，訊息直接給使用者。`ForbiddenException`、`NotFoundException`、`ConcurrencyException`。
- `BusinessExceptionFilter` 統一處理：403 / 404 / 回上一頁並顯示訊息。Controller 不需 try/catch。
- 成功或錯誤提示用 `TempData[Flash.Success]` / `TempData[Flash.Error]`。
- **表單送出例外**：用 `this.TryExecute(() => service.Xxx())` 把 `BusinessRuleException` 放進 `ModelState` 以保留輸入；其他例外仍交給過濾器。
- 模型繫結的中文訊息設定於 `Program.cs`；ViewModel 驗證屬性一律寫中文 `ErrorMessage`。
- Action 參數名稱不可與其 ViewModel 的屬性同名（例如含 `Model` 屬性的表單，參數命名 `form`）。

## 授權與安全
- 全站預設需登入（fallback policy）；匿名端點明確標 `[AllowAnonymous]`。
- 角色限制用 `[Authorize(Roles = Roles.Admin | Roles.Managers | Roles.Staff)]`。
- Service 用 `currentUser.RequireAdmin/RequireManager/RequireStaff/RequireUserId`；檢視用 `User.IsManager()`、`User.IsStaff()`。
- **Service 層也要檢查**角色與資料擁有權（用 `ICurrentUser`），不可只靠 Controller 或隱藏按鈕。
- CSRF：全域 `AutoValidateAntiforgeryToken`，表單用 tag helper。XSS：只用 Razor 編碼輸出，禁止對使用者資料用 `Html.Raw`；JS 寫 DOM 用 `textContent`。
- Cookie 每次要求驗證帳號啟用與 `SecurityStamp`；停用、改密碼、改角色時要更換 `SecurityStamp`。
- 登入失敗鎖定次數與時間讀自 AppSettings。
- 稽核：呼叫 `IAuditService.Record(AuditActions.Xxx, nameof(實體), id, 名稱, 說明)`；新動作代碼登錄於 `AuditActions`。

### 權限矩陣（階段 2）
| 功能 | 申請人 | 技術員 | 主管 | 管理員 |
|---|---|---|---|---|
| 資產清單、詳情（基本資料、保固、位置、保養計畫） | ✓ | ✓ | ✓ | ✓ |
| 詳情的維修歷史 | 僅自己的 | 全部 | 全部 | 全部 |
| 轉移紀錄、附件檢視與上傳、列印標籤 | | ✓ | ✓ | ✓ |
| 刪除附件 | | 僅自己上傳的 | ✓ | ✓ |
| 成本與折舊、資產新增／編輯／轉移／報廢、匯入匯出、類別與地點 | | | ✓ | ✓ |
| 使用者管理 | | | | ✓ |

## 檔案上傳
- 允許的副檔名與大小讀自 AppSettings，且只能是 `AttachmentService.KnownTypes` 內的類型；檢查檔案開頭特徵碼，Content-Type 由伺服器決定。
- 實體檔以隨機名稱存放；下載一律經 `/Attachments/Download/{id}` 授權（圖片內嵌、其他下載、`nosniff`）。
- 工單附件的存取規則尚未建立（目前回 404），階段 3 於 `AttachmentService` 擴充。

## 開發環境工具
- 快速切換使用者（`POST /Dev/SwitchUser`）：僅 Development，預設僅本機連線；手機測試需要時設 `DevTools:AllowRemoteQuickSwitch=true`（注意：同網段任何人都能切成管理員）。
- 重置範例資料（`POST /Admin/ResetData`）：**僅 Development**、僅管理員、寫稽核、完成後回登入頁。
- 開發用工具加 `[DevelopmentOnly]`。

## 範例資料
- 文字與情境定義在 `SeedData/_source/catalog.json`（含示範密碼 `demoPassword`）；程式碼不寫死範例內容。
- 產生（僅 Development，固定亂數種子，日期相對執行當下；驗證失敗不寫檔）：
  `dotnet run --project AssetCare -- --generate-seed`
- 人工修改後檢查：`dotnet run --project AssetCare -- --validate-seed`
- 日期是產生當下的快照，放久後工單會逾期；需要新鮮資料時重新產生再重置。密碼雜湊鹽值每次不同。

## 已定案的商業規則解讀
- 工單狀態轉移唯一定義於 `WorkOrderStateMachine`。
- 逾時判斷用 `SlaRules.IsOverdue`：剛好等於時限不算逾時。
- 回應時間 `RespondedAt` = 首次派工或接單。
- 只有**報修**工單影響資產狀態：有任一未結案報修工單 → `UnderRepair`；全部結案或取消後回復。保養工單不改資產狀態。
- 保養工單沒有回應時限，完工時限 = 保養日當天 23:59:59（台北）。`RequesterUserId` 為 null。
- 同一保養計畫以 `MaintenanceCycleKey`（`yyyy-MM-dd` 或 `usage:數值`）防重複。
- 驗收退回：`CompletedAt` 清空、`ReworkCount`+1，保留前次完工填寫內容。
- 工單日誌：狀態變更記 `FromStatus`/`ToStatus`；留言、領料、外送等兩者皆 null。
- `PartTransaction.Quantity` 帶正負號，`BalanceAfter` 為異動後庫存。
- 單號（`WO`、`PM` 於階段 3、4 實作）：`WO-yyyyMMdd-0001`、`PM-yyyyMMdd-0001`（台北日期）、`AST-類別代碼-0001`；以鎖內取現有最大序號 +1 配號。
- 資產編號建立後不變，因此類別不可改、使用中的類別代碼不可改。資產不提供刪除（用報廢）。
- 資產地點、部門、保管人只能透過「轉移」變更（會留紀錄）。編輯只能在使用中／閒置間切換；維修中由工單控制。
- 報廢需原因、無法復原；有未結案工單時拒絕；同時停用該資產的保養計畫。已報廢資產用 `AssetRules.EnsureNotScrapped` 擋下編輯、轉移、再報廢；**階段 3 開立工單、階段 4 建立保養計畫與產生工單也必須呼叫**。
- 折舊用 `DepreciationCalculator`：直線法、按滿月、四捨五入到元，帳面價值介於殘值與成本之間；報廢資產計算到報廢日。
- 保固狀態用 `WarrantyRules`：到期日當天仍在保固內，剩餘天數 ≤ `warranty.noticeDays` 為即將到期。
- QR 標籤內容為 `{網址}/Assets/ByNo/{資產編號}`（用編號，重置資料後仍有效）。網址取 `App:PublicBaseUrl`，空白時用目前請求的主機。**階段 3 的掃碼報修沿用此入口**。
- 非空白的序號不可重複（僅在新增、匯入、序號變更時檢查）。
- Excel 匯入全有或全無，上限 500 列；欄位定義於 `AssetExcelService.Columns`，匯出欄位為其超集。
- 密碼原則 `PasswordRules`：最短長度讀設定、需含英文與數字、不可包含帳號、最長 100。
- 使用者不提供刪除（用停用）。帳號建立後不可改。不可停用自己或改自己的角色，至少保留一位啟用中的管理員。技術員有未結案工單時不可改角色；停用不阻擋，僅提醒交接。
- 站內連結格式：`/WorkOrders/Details/{id}`、`/Assets/Details/{id}`、`/Parts/Details/{id}`、`/MaintenancePlans/Details/{id}`（範例通知已使用，Controller 命名需一致）。

## 前端與版型
- 函式庫放 `wwwroot/lib`，不用 CDN：bootstrap 5.3、bootstrap-icons、echarts、html5-qrcode、fullcalendar（含 `zh-tw.global.min.js`）、jquery 與表單驗證。
- 樣式寫在 `wwwroot/css/site.css`，主色 `--app-primary`；不使用 scoped CSS。共用 JS 在 `wwwroot/js/site.js`。
- 版型 `_Layout`：`lg` 以上用上方導覽，以下用 `_BottomNav`（首頁、掃碼、我的工單、通知、我）。新增桌面選單項目時，手機需能從首頁或「我」頁到達。
- 手機：清單用卡片呈現（`d-lg-none` 卡片 + `d-none d-lg-block` 表格），觸控目標 ≥ 44px。
- 清單查詢條件用 `XxxQuery`（GET 繫結），回傳 `PagedResult<T>`（每頁 10 筆），分頁用 `<partial name="_Pager" model="PagerViewModel.From(result)" />`。
- 下拉選單資料用 `Option(Id, Text)` + `.ToSelectList()`；狀態標籤用 `.BadgeClass()`；詳情欄位用 `.detail-list`。
- 日期欄位的 `input` 需明確寫 `type="date"`（可為 null 的 `DateOnly` 不會自動套用）。
- `site.js` 提供 `data-print`、`data-check-all` + `data-require-checked` / `data-needs-checked`、`data-life-target`。
- A4 標籤樣式為 `site.css` 的 `.label-sheet`；`@page` 設定寫在 `Assets/Labels.cshtml`。
- 危險操作表單加 `data-confirm="訊息"`（可加 `data-confirm-title`），由 `site.js` 顯示確認視窗。
- 空狀態用 `.empty-state`；摘要卡片用 `.stat-card`。
- 尚未開放的功能暫用 `ComingSoon` 檢視；實作時替換該 action。

## 記錄與健康檢查
- Serilog：Console + `logs/assetcare-yyyyMMdd.log`（保留 30 天）。
- `GET /health`（匿名）：確認資料目錄可寫。
