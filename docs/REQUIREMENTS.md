你是資深 .NET 架構師。我已用 Visual Studio 建立好空白的 ASP.NET Core MVC 專案（Solution: AssetCare，專案: AssetCare.Web），請沿用該專案的 .NET 版本，不要升降級。這是全新對話，請以本文為完整需求。

請在這個專案上實作一套「公司內部可實際使用」的設備資產報修與保養管理系統，功能要完整，不是教學範例或簡化版。桌機與手機（RWD）都要好用，手機是一等公民（現場人員用手機掃 QR Code 報修與回報）。

## 工作方式（重要，請嚴格遵守）
- 分 4 個階段進行，每階段完成後停下來，等我說「繼續」再做下一階段
- 每階段開始前，先讀 docs/CONVENTIONS.md 與現有程式碼，沿用既有命名、資料夾與樣式，不要自行改名或重構已完成的部分
- 每階段直接把檔案寫入專案，不要在對話中貼完整程式碼；小修改只改有問題的地方，不要整檔重寫
- 每階段結束時專案必須能 build 並正常啟動，不留半成品
- 每階段結束前，依「階段自我檢查」逐項確認，並更新 docs/CONVENTIONS.md
- 每階段完成後只回報：新增或修改的檔案摘要、需要執行的指令、如何驗證、自我檢查結果、已知未完成項目

## 基本限制
- 全部使用免費套件；前端函式庫（Bootstrap 5、Bootstrap Icons、ECharts、QR Code 掃描用的 html5-qrcode）放 wwwroot/lib，不依賴 CDN，可離線執行
- QR Code 產生使用 QRCoder，Excel 匯出使用 ClosedXML，PDF（標籤列印）可用瀏覽器列印樣式，不引入付費 PDF 套件
- UI 繁體中文，時間顯示用 Asia/Taipei
- 不使用任何付費服務或 API Key
- 不使用資料庫，不使用 EF Core
- 登入使用 Cookie Authentication + PasswordHasher，角色式授權
- 不要建立測試專案，不要寫單元測試，不要建立 CI 設定檔

## 命名與結構規則（階段 1 寫入 docs/CONVENTIONS.md，之後全部遵守）
- 實體（Domain/Entities，皆繼承 BaseEntity：Id(Guid)、Version(int)、CreatedAt、UpdatedAt）：User、Department、Location、AssetCategory、Asset、AssetTransfer、Vendor、WorkOrder、WorkOrderLog、WorkOrderPart、MaintenancePlan、Part、PartTransaction、Notification、EmailLog、Attachment、AuditLog、AppSetting
- 列舉：UserRole { Requester, Technician, Supervisor, Admin }、AssetStatus { InUse, Idle, UnderRepair, Scrapped }、WorkOrderStatus { Open, Assigned, InProgress, WaitingParts, Completed, Closed, Cancelled }、WorkOrderType { Repair, Preventive }、Priority { Low, Normal, High, Urgent }、MaintenanceCycle { Daily, Weekly, Monthly, Quarterly, Yearly, ByUsage }
- 資料夾：Domain/ Application/（Services、Interfaces、Dtos）/ Infrastructure/（Json、Background、Email）/ Web/（Controllers、Views、ViewModels、ViewComponents）
- Service 命名 XxxService，介面 IXxxService；Controller 命名 XxxController；JSON 檔名為實體名複數；JSON 欄位 camelCase
- 時間一律以 UTC 儲存，顯示才轉 Asia/Taipei；以集中的 ITimeProvider 取得現在時間，不得直接用 DateTime.Now

## 資料層（JSON Mock）
- 定義簡單的 IRepository<T>（GetAll、GetById、Query、Add、Update、Delete），Controller 只依賴 Service，Service 只依賴此介面，不得出現 JSON 相關型別，讓日後換成資料庫實作時商業邏輯不需修改
- 實作 JsonFileRepository<T>：啟動時載入記憶體，異動時以「寫暫存檔再取代」方式寫回，執行緒安全
- Mock 資料放 SeedData/*.json（進 Git，每個實體一個檔案，縮排排版，方便人工修改）；首次啟動複製到 App_Data/（加入 .gitignore）
- 每個實體有 Version 欄位做樂觀並行控制，衝突時顯示友善提示
- 附件與照片實體檔存 App_Data/uploads，JSON 只存中繼資料
- Mock 資料只能在 JSON，程式碼不得寫死

## 核心商業規則（請嚴格照此實作）
1. 工單狀態轉移只允許：Open→Assigned→InProgress→(WaitingParts↔InProgress)→Completed→Closed；Open 或 Assigned 可 Cancelled；Completed 被申請人驗收不通過可退回 InProgress；非法轉移一律拒絕並顯示原因，每次轉移寫入 WorkOrderLog
2. 工單開立時資產狀態變 UnderRepair（可設定是否自動），工單 Closed 時回到 InUse；資產 Scrapped 後不可再開工單
3. 單號：報修 WO-yyyyMMdd-0001、保養 PM-yyyyMMdd-0001、資產編號 AST-類別代碼-0001，以序號防併發重複
4. SLA：依優先級設定「回應時限」與「完工時限」（Urgent 2 小時／8 小時、High 4／24、Normal 8／72、Low 24／168，可在設定頁修改）；超過未回應或未完工標示逾時，BackgroundService 每 5 分鐘掃描，逾時通知主管
5. 預防保養：MaintenancePlan 依週期（日、週、月、季、年、依使用量）自動產生保養工單，產生時間為「下次保養日前 N 天」（N 可設定）；同一計畫同一週期不可重複產生；工單完成後依完成日重算下次保養日
6. 零件：領用工單零件時扣庫存，庫存不足不可領用；庫存低於安全量時通知主管並在清單標示
7. 派工：技術員只能處理指派給自己的工單；主管可改派；技術員可「接單」未指派的工單
8. 資產折舊：直線法，依購入日、成本、殘值、耐用年限計算目前帳面價值

## Mock 資料
請寫一支 Development 限定的產生腳本（固定亂數種子）批次產生 JSON，不要手寫。原則：每一種情況只放 2 筆，足夠 demo 即可，不要灌量，但每個功能都要有資料能 demo：
- 使用者 10 位（Requester 4、Technician 3、Supervisor 2、Admin 1，含 1 位已停用）、部門 4 個、地點 6 個（含樓層階層）
- 資產類別 5 種（空調、電腦周邊、生產設備、消防安全、辦公家具）、資產 20 筆（涵蓋所有狀態、含已報廢 2 筆、含保固即將到期 2 筆、含已過保固 2 筆）、轉移紀錄 4 筆、廠商 5 家
- 工單：每種狀態（Open、Assigned、InProgress、WaitingParts、Completed、Closed、Cancelled）各 2 筆；另有逾時 2 筆、Urgent 2 筆、預防保養工單 2 筆、被退回重做 2 筆、含照片附件 2 筆。總計約 25 筆，每張有完整處理歷程（WorkOrderLog）
- 保養計畫 6 筆（含已逾期未保養 2 筆、即將到期 2 筆）、零件 10 種（含低於安全量 2 種）、零件異動 10 筆
- 通知 15 筆、Email 寄送紀錄 8 筆、稽核紀錄 15 筆
- 日期一律「相對於執行當天」產生，避免放久後全部逾期
- 產生後腳本需自我驗證：所有 Id 參照存在、工單狀態與資產狀態一致、狀態歷程順序合法，驗證失敗要中止並提示
- 內容要像真實公司：設備名稱、故障描述、人名都要合理，不要 test1 或 Lorem ipsum

## 重置測試資料
- 管理頁與右上角工具列（Development 環境）都要有「重置為範例資料」按鈕
- 按下後有確認視窗，確認後清空 App_Data 並從 SeedData 重新複製，完成後回到登入頁
- 僅管理員可用，並寫入稽核紀錄

## 功能
1. 資產管理：資產清單（搜尋、篩選、分頁、排序）；資產詳情（基本資料、保固、折舊帳面價值、位置、保管人、維修歷史、保養計畫、轉移紀錄、附件）；新增、編輯、報廢（需填原因）；轉移（變更保管人或地點並留紀錄）；批次匯入 Excel；匯出 Excel；資產類別與地點維護；QR Code 標籤產生（單張與批次列印，A4 多格排版，含資產編號與名稱）
2. 報修與工單：手機掃 QR Code 進入該資產報修頁（免輸入資產資料，登入後帶入）；報修表單（故障描述、優先級、拍照上傳最多 5 張）；工單清單（我的報修、待派工、我的待辦、全部，搜尋篩選分頁）；工單詳情（資產資訊、處理歷程時間軸、照片、使用零件、花費工時）；派工、改派、接單、開始處理、待料、完工填寫（原因、處理方式、工時、零件）、申請人驗收或退回、取消；工單留言；相同資產 7 天內重複報修時提示並連結既有工單
3. 預防保養：保養計畫維護（資產、週期、提前產生天數、負責人、保養檢查清單）；自動產生保養工單；保養工單的檢查清單逐項勾選；保養行事曆（月視圖，FullCalendar 放 wwwroot/lib）；逾期保養提醒
4. 零件與廠商：零件庫存管理、領用與入庫、安全庫存警示、異動紀錄；廠商資料維護；外送維修（工單可指定廠商與外送日期、預計回廠日）
5. 通知：站內通知中心（未讀紅點）；事件含新報修、被派工、工單狀態變更、驗收結果、SLA 逾時、保養即將到期、庫存低於安全量、保固即將到期；Email 用寫入 outbox 資料夾的 .eml 檔模擬，保留寄送紀錄
6. 儀表板與報表：依角色顯示不同儀表板（申請人看我的報修、技術員看我的待辦與今日保養、主管看全局）；主管儀表板含工單數量趨勢、故障類別分布、平均回應與完工時間、SLA 達成率、設備故障排行（MTBF 簡易計算）、各部門報修量、維修成本統計（ECharts）；報表匯出 Excel（工單明細、資產清冊、保養紀錄、零件異動、維修成本）
7. 系統管理與安全：使用者管理（新增、停用、重設密碼、指派角色與部門）；SLA 與系統參數設定；稽核紀錄（誰、何時、IP、對象、動作，可查詢與匯出）；CSRF、XSS 防護；上傳白名單（副檔名與大小）；授權檢查在伺服器端（Controller 與 Service 層級，不能只靠隱藏按鈕）；密碼原則與登入失敗鎖定；Serilog（Console + logs/）、Health Check、友善錯誤頁與全域例外處理；Development 環境提供「快速切換使用者」下拉

## 手機版
- 工單清單以卡片呈現，底部導覽（首頁、掃碼、我的工單、通知、我）
- 「掃碼」頁用 html5-qrcode 開鏡頭掃描，掃到後導向該資產頁（可報修、查看歷史）；掃不到時提供手動輸入資產編號
- 技術員的接單、開始、完工回報、拍照上傳要能單手操作；完工表單欄位精簡
- 若瀏覽器不支援相機（非 HTTPS 時常見），要有友善提示與替代方式，並在 README 說明手機測試方式（同網段使用 HTTPS 開發憑證）

## 階段劃分
- 階段 1：專案骨架、套件、docs/CONVENTIONS.md、Domain 實體與列舉、ITimeProvider、IRepository 與 JsonFileRepository、Mock 資料產生腳本與 SeedData（含自我驗證）、Cookie 登入與角色授權、共用版型與 RWD 框架（含手機底部導覽）、重置按鈕、快速切換使用者
- 階段 2：資產管理（清單、詳情、新增編輯、報廢、轉移、類別與地點維護、折舊、QR Code 標籤、Excel 匯入匯出）、使用者管理
- 階段 3：報修與工單（掃碼報修、工單全流程、狀態機、派工接單、驗收、留言、照片、重複報修提示）、零件與廠商、外送維修、通知中心與 Email 模擬、SLA 背景掃描
- 階段 4：預防保養（計畫、自動產生、檢查清單、行事曆）、儀表板（依角色）、報表 Excel、系統參數、稽核紀錄、安全強化、README.md（簡介、功能清單、截圖占位、啟動方式最多 3 行、Demo 帳號、手機測試方式、Mermaid 架構圖與工單狀態圖、JSON 欄位說明、日後如何改接資料庫的簡短說明）、docs/notes.md（設計取捨、已知限制、未完成項目）、.gitignore（含 App_Data/、logs/、outbox/）

## 階段自我檢查（每階段結束前逐項確認，結果列在回報中）
- 專案 build 無錯誤，啟動後首頁可開
- 本階段新增的 Controller action 都有伺服器端授權檢查（Requester 不能進入主管與管理頁面，技術員不能處理他人工單）
- 所有時間比較都用 UTC 與 ITimeProvider，顯示才轉台北時間
- 階段 2 額外檢查：報廢資產不可再轉移或開工單、資產編號不重複、QR Code 內容導向正確資產頁、折舊計算不會出現負值
- 階段 3 額外檢查：每個非法狀態轉移都被拒絕、工單與資產狀態同步、零件庫存不足不可領用、SLA 逾時判斷邊界正確、同一資產 7 天內重複報修有提示
- 階段 4 額外檢查：同一保養計畫同一週期不會重複產生工單、完成保養後下次保養日重算正確、各角色儀表板看到的資料範圍正確
- 本階段新增的命名與規則已更新到 docs/CONVENTIONS.md
