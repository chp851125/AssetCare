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
