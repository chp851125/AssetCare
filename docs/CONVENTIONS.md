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
| `Application/Services`、`Interfaces`、`Dtos` | `AssetCare.Application.*` | 商業邏輯；`AuditActions`、`SettingKeys` 與共用輔助（`CurrentUserExtensions`、`SettingExtensions`、`InputText`、`EnumNames`、`WorkOrderAccess`、`WorkOrderLinks`）放 `Application/` 根目錄 |
| `Infrastructure/Json`（含 `Seeding`）、`Excel`、`Files`、`Qr`、`Background`、`Email` | `AssetCare.Infrastructure.*` | JSON 資料層、種子工具；ClosedXML、附件實體檔、QRCoder 的轉接；`ScanBackgroundService`、`OutboxEmailSender` |
| `Web/Controllers`、`Views`、`ViewModels`、`ViewComponents`、`Infrastructure` | `AssetCare.Web.*` | 展示層；`Web/Infrastructure` 放驗證、過濾器、擴充方法 |
| `SeedData/` | — | 範例資料（進 Git） |
| `App_Data/`、`logs/`、`outbox/` | — | 執行期資料（不進 Git） |

相依方向：Controller / ViewComponent → Service 介面 → `IRepository<T>`。Application 與 Domain **不得出現 JSON 或 Infrastructure 型別**。
第三方套件（ClosedXML、QRCoder）與檔案系統一律經由 Application 介面（`ISpreadsheetService`、`IQrCodeGenerator`、`IFileStorage`、`IEmailSender`）。

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

### 權限矩陣（資產與系統管理）
| 功能 | 申請人 | 技術員 | 主管 | 管理員 |
|---|---|---|---|---|
| 資產清單、詳情（基本資料、保固、位置、保養計畫） | ✓ | ✓ | ✓ | ✓ |
| 詳情的維修歷史 | 僅自己的 | 全部 | 全部 | 全部 |
| 轉移紀錄、附件檢視與上傳、列印標籤 | | ✓ | ✓ | ✓ |
| 刪除附件 | | 僅自己上傳的 | ✓ | ✓ |
| 成本與折舊、資產新增／編輯／轉移／報廢、匯入匯出、類別與地點 | | | ✓ | ✓ |
| 使用者管理 | | | | ✓ |

### 權限矩陣（工單、零件、通知）
| 功能 | 申請人 | 技術員 | 主管 | 管理員 |
|---|---|---|---|---|
| 報修、自己的工單（檢視、留言、照片） | ✓ | ✓ | ✓ | ✓ |
| 他人的工單 | 僅近期重複報修的摘要 | 檢視、留言 | ✓ | ✓ |
| 清單分頁 | 我的報修 | 我的待辦、待派工、我的報修、全部 | 待派工、全部、我的報修 | 同主管 |
| 接單（未指派） | | ✓ | | |
| 開始、待料、恢復、完工、領料、外送、送回 | | 僅指派給自己 | ✓ | ✓ |
| 派工、改派 | | | ✓ | ✓ |
| 驗收、退回、取消 | 僅自己申請的 | 僅自己申請的 | ✓ | ✓ |
| 零件清單與詳情 | | ✓ | ✓ | ✓ |
| 零件新增／編輯／入庫／盤點、廠商維護 | | | ✓ | ✓ |
| 通知中心 | 僅自己的 | 僅自己的 | 僅自己的 | 僅自己的 |

- 清單中每個角色的**第一個分頁即預設分頁**（`WorkOrderService.AvailableScopes`）。
- 工單資料擁有權一律用 `WorkOrderAccess`（`CanView`、`CanProcess`、`CanVerify`）。

## 檔案上傳
- 允許的副檔名與大小讀自 AppSettings，且只能是 `AttachmentService.KnownTypes` 內的類型；檢查檔案開頭特徵碼，Content-Type 由伺服器決定。
- 實體檔以隨機名稱存放；下載一律經 `/Attachments/Download/{id}` 授權（圖片內嵌、其他下載、`nosniff`）。
- 工單照片：僅圖片類型、一次最多 5 張；申請人、負責技術員、主管可上傳（限未結案工單）；可完整檢視工單者才能下載；上傳者本人（未結案時）或主管可刪除。
- 與其他異動一起送出的照片，先用 `AttachmentService.CheckPhotos` 全部檢查，主要資料寫入後再 `Save`。
- 含多檔上傳的 action 加 `[RequestSizeLimit]`（55 MB）；畫面用 `_PhotoInput`。

## 工單
- 讀寫分離：`IWorkOrderService`（查詢）、`IWorkOrderWorkflowService`（所有異動）。工單狀態**只能**由 workflow service 變更。
- 動作是否允許由 `WorkOrderStateMachine.Allows(動作, 目前狀態)` 判斷（不可只看目標狀態：「恢復處理」與「驗收退回」都回到處理中）。拒絕時用 `WorkOrderWorkflowService.EnsureAllowed` 的訊息。
- 狀態：派工／接單 Open→Assigned；開始 Assigned→InProgress；待料 InProgress↔WaitingParts；完工 InProgress→Completed；驗收 Completed→Closed；退回 Completed→InProgress；取消 Open、Assigned→Cancelled。
- 不改狀態的動作：改派（Assigned／InProgress／WaitingParts）、領料（InProgress／WaitingParts）、外送與送回（Assigned／InProgress／WaitingParts，需已指派）。留言任何狀態皆可。
- 每個異動寫一筆 `WorkOrderLog`；表單帶 `version`，工單 `Update` 排在扣庫存等其他寫入之前。
- 派工、改派、取消寫稽核（開單亦寫）；其餘處理步驟以工單歷程為準。
- 工單標題取故障描述第一行（最長 40 字），報修表單不另填標題。
- 設備在廠商處（有廠商且無回廠日）時不可完工、取消或再次外送。多次外送時 `VendorCost` 累計。
- 「近期重複報修」：同資產、未取消的報修工單，建立時間在 `workOrder.duplicateReportDays` 天內（剛好滿天數仍算）；只提示不阻擋。
- 逾時顯示用 `WorkOrderRules.IsResponseOverdue／IsResolveOverdue`（已標示或即時判斷）；取消的工單只保留既有標示。

## 零件
- 庫存只能經由入庫、工單領用、盤點調整變更，每次寫一筆 `PartTransaction`。領用**一定要指定工單**（零件頁領用亦同）。
- 扣庫存流程：鎖內先 `PartService.CheckAvailability`（同零件多列合併），再 `Consume`；鎖外 `NotifyLowStock`。
- 低庫存用 `PartRules.IsLowStock`（啟用中且庫存 < 安全量）；只在「由正常變低」時通知。
- 入庫單價會更新零件單價；用料成本以領用當下單價為準。零件與廠商不刪除（用停用）。料號 `PRT-0001`。

## 通知與背景作業
- 發通知一律用 `INotificationDispatcher.Send`（自動排除操作者本人與停用帳號，同時寫 .eml 與 `EmailLog`），在 `IBusinessLock` 之外呼叫。
- `.eml` 寫入 `Email:OutboxDirectory`（預設 `outbox/`）；寄件人設定於 `appsettings.json` 的 `Email`。郵件內連結前綴為 `App:PublicBaseUrl`（空白時為相對路徑）。
- `ScanBackgroundService`：啟動時及每 5 分鐘執行 `ISlaService.ScanOverdue` 與 `IReminderService.SendDueReminders`。背景呼叫的服務不可依賴登入身分。
- SLA 逾時各標示一次；已結案者只標示不通知。到期提醒以「類型＋連結＋含日期的標題」避免重複。
- 掃描標示逾時會更新工單版本，開著舊畫面送出時會看到並行衝突提示。

## 掃碼
- QR 入口 `/Assets/ByNo/{編號}` 導向 `/WorkOrders/Create?assetId=`（已報廢則轉資產詳情並說明）。
- 系統內掃碼頁（`wwwroot/js/scan.js`）只取出資產編號，經 `/Scan/Asset?code=` 導向**資產詳情**（可報修、查歷史）；不直接開啟掃到的網址。
- 相機需 HTTPS 或 localhost。手機測試：`dotnet run --project AssetCare --launch-profile lan-https`，手機開 `https://電腦IP:7171`（需信任開發憑證；完整說明於階段 4 寫入 README）。


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
- 單號：`WO-yyyyMMdd-0001`、`PM-yyyyMMdd-0001`（台北日期，`WorkOrderRules.NextOrderNo`；PM 由階段 4 使用）、`AST-類別代碼-0001`；以鎖內取現有最大序號 +1 配號。
- 資產編號建立後不變，因此類別不可改、使用中的類別代碼不可改。資產不提供刪除（用報廢）。
- 資產地點、部門、保管人只能透過「轉移」變更（會留紀錄）。編輯只能在使用中／閒置間切換；維修中由工單控制。
- 報廢需原因、無法復原；有未結案工單時拒絕；同時停用該資產的保養計畫。已報廢資產用 `AssetRules.EnsureNotScrapped` 擋下編輯、轉移、再報廢；開立工單已呼叫；**階段 4 建立保養計畫與產生工單也必須呼叫**。
- 折舊用 `DepreciationCalculator`：直線法、按滿月、四捨五入到元，帳面價值介於殘值與成本之間；報廢資產計算到報廢日。
- 保固狀態用 `WarrantyRules`：到期日當天仍在保固內，剩餘天數 ≤ `warranty.noticeDays` 為即將到期。
- QR 標籤內容為 `{網址}/Assets/ByNo/{資產編號}`（用編號，重置資料後仍有效）。網址取 `App:PublicBaseUrl`，空白時用目前請求的主機。
- 非空白的序號不可重複（僅在新增、匯入、序號變更時檢查）。
- Excel 匯入全有或全無，上限 500 列；欄位定義於 `AssetExcelService.Columns`，匯出欄位為其超集。
- 密碼原則 `PasswordRules`：最短長度讀設定、需含英文與數字、不可包含帳號、最長 100。
- 使用者不提供刪除（用停用）。帳號建立後不可改。不可停用自己或改自己的角色，至少保留一位啟用中的管理員。技術員有未結案工單時不可改角色；停用不阻擋，僅提醒交接。
- 保養工單可走一般工單流程；檢查清單目前唯讀。**階段 4 須補：逐項勾選、完工後重算計畫下次保養日**（掛在 `WorkOrderWorkflowService.Complete`），並實作 `MaintenancePlansController.Details`（目前為預留頁）。
- 站內連結格式：`/WorkOrders/Details/{id}`、`/Assets/Details/{id}`、`/Parts/Details/{id}`、`/MaintenancePlans/Details/{id}`（範例通知已使用，Controller 命名需一致）。

## 前端與版型
- 函式庫放 `wwwroot/lib`，不用 CDN：bootstrap 5.3、bootstrap-icons、echarts、html5-qrcode、fullcalendar（含 `zh-tw.global.min.js`）、jquery 與表單驗證。
- 樣式寫在 `wwwroot/css/site.css`，主色 `--app-primary`；不使用 scoped CSS。共用 JS 在 `wwwroot/js/site.js`，單頁腳本另立檔案（如 `scan.js`）。
- 手機主要操作按鈕放 `.action-bar`（固定在底部導覽上方）。處理歷程用 `.timeline`。優先級標籤用 `Priority.BadgeClass()`。
- `site.js`：POST 表單送出後自動停用按鈕防重複送出；`data-max-files`／`data-max-mb`／`data-preview` 檢查並預覽照片；`data-scroll-to` 展開後捲動。
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
