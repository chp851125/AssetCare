// 保養行事曆（FullCalendar）。窄螢幕改用清單檢視。
(() => {
    const element = document.getElementById('maintenanceCalendar');
    if (!element || !window.FullCalendar) return;
    const message = document.getElementById('calendarError');
    const narrow = window.matchMedia('(max-width: 767.98px)');
    const view = () => narrow.matches ? 'listMonth' : 'dayGridMonth';
    // 窄螢幕只留切換月份與「今天」，避免標題被擠成兩行。
    const toolbar = () => narrow.matches
        ? { left: 'prev,next', center: 'title', right: 'today' }
        : { left: 'prev,next today', center: 'title', right: 'dayGridMonth,listMonth' };

    const calendar = new FullCalendar.Calendar(element, {
        locale: 'zh-tw',
        initialView: view(),
        // 「今天」以伺服器提供的台北日期為準。
        now: element.dataset.today,
        initialDate: element.dataset.today,
        height: 'auto',
        dayMaxEvents: 3,
        headerToolbar: toolbar(),
        buttonText: { month: '月', list: '清單' },
        noEventsContent: '本月沒有保養排程。',
        events: {
            url: element.dataset.events,
            failure: () => message.classList.remove('d-none')
        },
        eventDataTransform: event => ({
            title: event.title, start: event.start, url: event.url, allDay: true,
            classNames: [`event-${event.kind}`]
        }),
        loading: busy => { if (busy) message.classList.add('d-none'); }
    });
    calendar.render();
    narrow.addEventListener('change', () => {
        calendar.setOption('headerToolbar', toolbar());
        calendar.changeView(view());
    });
})();
