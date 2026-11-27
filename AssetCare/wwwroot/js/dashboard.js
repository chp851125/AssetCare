// 主管儀表板圖表。資料由 #dashboard 的 data-charts 帶入；只用 ECharts 內建提示框（會自動編碼文字）。
(() => {
    const root = document.getElementById('dashboard');
    if (!root || !window.echarts) return;
    const data = JSON.parse(root.dataset.charts);
    const primary = '#14532d';
    const palette = [primary, '#0d6efd', '#fd7e14', '#6f42c1', '#dc3545', '#20c997', '#6c757d'];
    const charts = [];

    const draw = (name, count, option) => {
        const element = root.querySelector(`[data-chart="${name}"]`);
        if (!element) return;
        if (count === 0) {
            element.classList.add('chart-empty');
            element.textContent = '期間內沒有資料。';
            return;
        }
        const chart = echarts.init(element);
        chart.setOption({ color: palette, ...option });
        charts.push(chart);
    };
    const grid = { left: 8, right: 16, top: 40, bottom: 8, containLabel: true };
    const countAxis = { type: 'value', minInterval: 1 };

    draw('trend', data.trend.length, {
        tooltip: { trigger: 'axis' },
        legend: { top: 0 },
        grid,
        xAxis: { type: 'category', data: data.trend.map(p => p.label) },
        yAxis: countAxis,
        series: [
            { name: '報修', type: 'line', data: data.trend.map(p => p.repair) },
            { name: '預防保養', type: 'line', data: data.trend.map(p => p.preventive) }
        ]
    });

    draw('categories', data.categories.length, {
        tooltip: { trigger: 'item' },
        legend: { bottom: 0 },
        series: [{
            name: '報修件數', type: 'pie', radius: ['35%', '60%'], center: ['50%', '44%'],
            data: data.categories.map(c => ({ name: c.name, value: c.count })),
            label: { formatter: '{c}' }
        }]
    });

    draw('departments', data.departments.length, {
        tooltip: { trigger: 'axis' },
        grid: { ...grid, top: 16 },
        xAxis: { type: 'category', data: data.departments.map(d => d.name), axisLabel: { interval: 0, overflow: 'break', width: 70 } },
        yAxis: countAxis,
        series: [{ name: '報修件數', type: 'bar', barMaxWidth: 48, data: data.departments.map(d => d.count) }]
    });

    draw('costs', data.costs.some(c => c.parts + c.vendor > 0) ? data.costs.length : 0, {
        tooltip: { trigger: 'axis' },
        legend: { top: 0 },
        grid,
        xAxis: { type: 'category', data: data.costs.map(c => c.label) },
        yAxis: { type: 'value' },
        series: [
            { name: '用料成本', type: 'bar', stack: 'cost', barMaxWidth: 48, data: data.costs.map(c => c.parts) },
            { name: '外送費用', type: 'bar', stack: 'cost', barMaxWidth: 48, data: data.costs.map(c => c.vendor) }
        ]
    });

    // 類別軸由下往上排列，反轉後第一名在最上方。
    const ranking = [...data.ranking].reverse();
    draw('ranking', ranking.length, {
        tooltip: { trigger: 'axis' },
        grid: { ...grid, top: 8 },
        xAxis: countAxis,
        yAxis: { type: 'category', data: ranking.map(r => r.assetNo) },
        series: [{ name: '故障次數', type: 'bar', barMaxWidth: 24, data: ranking.map(r => r.count) }]
    });

    window.addEventListener('resize', () => charts.forEach(chart => chart.resize()));
})();
