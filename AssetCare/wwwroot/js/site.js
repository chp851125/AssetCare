// 任何帶有 data-confirm 的表單，送出前顯示確認視窗。
(() => {
    const element = document.getElementById('confirmModal');
    if (!element) return;
    const modal = new bootstrap.Modal(element);
    let pendingForm = null;

    document.addEventListener('submit', event => {
        const form = event.target;
        if (!form.dataset.confirm || event.defaultPrevented) return;
        event.preventDefault();
        pendingForm = form;
        document.getElementById('confirmModalTitle').textContent = form.dataset.confirmTitle || '請確認';
        document.getElementById('confirmModalBody').textContent = form.dataset.confirm;
        modal.show();
    });

    document.getElementById('confirmModalOk').addEventListener('click', event => {
        if (!pendingForm) return;
        event.currentTarget.disabled = true; // 避免重複送出
        pendingForm.submit();
    });

    element.addEventListener('hidden.bs.modal', () => {
        pendingForm = null;
        document.getElementById('confirmModalOk').disabled = false;
    });
})();

// 防止重複送出：POST 表單送出後停用按鈕（需確認的表單由確認視窗處理）。
document.addEventListener('submit', event => {
    const form = event.target;
    if (event.defaultPrevented || form.method !== 'post') return;
    if (form.dataset.submitting) { event.preventDefault(); return; }
    form.dataset.submitting = 'true';
    form.querySelectorAll('[type="submit"]').forEach(button => button.classList.add('disabled'));
});
// 由瀏覽器「上一頁」回到畫面時恢復按鈕。
window.addEventListener('pageshow', () => {
    document.querySelectorAll('form[data-submitting]').forEach(form => {
        delete form.dataset.submitting;
        form.querySelectorAll('[type="submit"]').forEach(button => button.classList.remove('disabled'));
    });
});

// 照片欄位：先在瀏覽器端檢查張數與大小（伺服器仍會檢查），並顯示縮圖。
document.querySelectorAll('input[type="file"][data-max-files]').forEach(input => {
    const preview = document.querySelector(input.dataset.preview);
    input.addEventListener('change', () => {
        preview?.querySelectorAll('img').forEach(image => URL.revokeObjectURL(image.src));
        preview?.replaceChildren();
        const files = [...input.files];
        const maxFiles = Number(input.dataset.maxFiles);
        const maxMb = Number(input.dataset.maxMb);
        let error = '';
        if (files.length > maxFiles) error = `一次最多 ${maxFiles} 張照片。`;
        else if (files.some(file => file.size > maxMb * 1024 * 1024)) error = `單張照片不可超過 ${maxMb} MB。`;
        input.setCustomValidity(error);
        if (error) { input.reportValidity(); return; }
        files.forEach(file => {
            const image = document.createElement('img');
            image.src = URL.createObjectURL(file);
            image.alt = file.name;
            preview?.append(image);
        });
    });
});

// 展開表單後捲動到該位置（觸發按鈕可能固定在畫面底部）。
document.querySelectorAll('[data-scroll-to]').forEach(button => {
    const target = document.querySelector(button.dataset.scrollTo);
    target?.addEventListener('shown.bs.collapse', () => {
        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
        target.querySelector('textarea, input:not([type="hidden"])')?.focus();
    });
});

// 列印按鈕。
document.addEventListener('click', event => {
    if (event.target.closest('[data-print]')) window.print();
});

// 全選核取方塊與「需勾選才能送出」的按鈕。清單同時有手機卡片與桌面表格，只處理目前可見者。
(() => {
    const visibleBoxes = (form, name) =>
        [...form.querySelectorAll(`input[type="checkbox"][name="${name}"]`)].filter(box => box.offsetParent !== null);

    document.querySelectorAll('form[data-require-checked]').forEach(form => {
        const name = form.dataset.requireChecked;
        const buttons = form.querySelectorAll('[data-needs-checked]');
        const refresh = () => {
            const any = form.querySelector(`input[name="${name}"]:checked`) !== null;
            buttons.forEach(button => { button.disabled = !any; });
        };
        form.addEventListener('change', event => {
            if (event.target.dataset.checkAll === name)
                visibleBoxes(form, name).forEach(box => { box.checked = event.target.checked; });
            refresh();
        });
        refresh();
    });
})();

// 選擇資產類別時帶入預設耐用年限（使用者自行輸入過則不覆蓋）。
document.querySelectorAll('select[data-life-target]').forEach(select => {
    const target = document.querySelector(select.dataset.lifeTarget);
    if (!target) return;
    let manual = target.value !== '';
    target.addEventListener('input', () => { manual = target.value !== ''; });
    select.addEventListener('change', () => {
        const life = select.selectedOptions[0]?.dataset.life;
        if (life && !manual) target.value = life;
    });
});

// 保養計畫表單：依週期顯示日期或使用量欄位，並限制提前產生天數上限。
document.querySelectorAll('select[data-cycle-select]').forEach(select => {
    const form = select.form;
    const lead = form.querySelector('[data-lead-days]');
    const hint = form.querySelector('[data-lead-hint]');
    const refresh = () => {
        const byUsage = select.value === 'ByUsage';
        form.querySelectorAll('[data-cycle-date]').forEach(field => field.classList.toggle('d-none', byUsage));
        form.querySelectorAll('[data-cycle-usage]').forEach(field => field.classList.toggle('d-none', !byUsage));
        const max = Number(select.selectedOptions[0]?.dataset.maxLead ?? 0);
        if (hint) hint.textContent = `此週期最多 ${max} 天。`;
        if (!lead) return;
        lead.max = String(max);
        if (Number(lead.value) > max) lead.value = String(max);
    };
    select.addEventListener('change', refresh);
    refresh();
});
