(() => {
    const form = document.getElementById('analyticsFilter');
    const results = document.getElementById('analyticsResults');
    if (!form || !results) return;
    const from = document.getElementById('analyticsFrom'), to = document.getElementById('analyticsTo');
    const loading = document.getElementById('analyticsLoading'), error = document.getElementById('analyticsError');
    let request = null, version = 0;
    const money = new Intl.NumberFormat('en-MY', {style:'currency',currency:'MYR',maximumFractionDigits:2});
    function svgNode(name, attrs = {}, text) {
        const node = document.createElementNS('http://www.w3.org/2000/svg', name);
        for (const [key,value] of Object.entries(attrs)) node.setAttribute(key,String(value));
        if (text !== undefined) node.textContent = text;
        return node;
    }
    function drawCharts() {
        const source = results.querySelector('.analytics-chart-data');
        if (!source) return;
        const rows = JSON.parse(source.textContent);
        for (const host of results.querySelectorAll('[data-analytics-chart]')) {
            host.replaceChildren();
            const trend = host.dataset.analyticsChart === 'trend';
            const width = Math.max(280, host.clientWidth || 900, rows.length * 65 + 120), height = 310, left = 95, right = 25, top = 25, bottom = 65;
            const plotW = width-left-right, plotH = height-top-bottom;
            const max = Math.max(1,...rows.map(r => trend ? r.expenses : Math.max(r.income,r.expenses)));
            const ceiling = max * 1.1, step = plotW / Math.max(1,rows.length);
            const x = i => left + step * (i+.5), y = value => top + plotH * (1-value/ceiling);
            const svg = svgNode('svg',{viewBox:`0 0 ${width} ${height}`,role:'img','aria-label':trend?'Monthly expense trend. Exact figures are available in the monthly figures table.':'Monthly income and expenses. Exact figures are available in the monthly figures table.'});
            svg.append(svgNode('title',{},trend?'Monthly spending trend':'Income versus expenses'));
            for (let i=0;i<=4;i++) {
                const value = ceiling*i/4, position = y(value);
                svg.append(svgNode('line',{x1:left,y1:position,x2:width-right,y2:position,stroke:'#dde6eb'}));
                svg.append(svgNode('text',{x:left-10,y:position+4,'text-anchor':'end',fill:'#526d7b','font-size':12},new Intl.NumberFormat('en-MY',{notation:'compact',maximumFractionDigits:1}).format(value)));
            }
            svg.append(svgNode('text',{x:left,y:14,fill:'#526d7b','font-size':12},'RM'));
            if (trend && rows.length > 1) svg.append(svgNode('polyline',{points:rows.map((r,i)=>`${x(i)},${y(r.expenses)}`).join(' '),fill:'none',stroke:'#16899a','stroke-width':3}));
            rows.forEach((row,i) => {
                svg.append(svgNode('text',{x:x(i),y:height-33,'text-anchor':'middle',fill:'#526d7b','font-size':rows.length>8?10:12},row.label));
                if (trend) {
                    const dot = svgNode('circle',{cx:x(i),cy:y(row.expenses),r:5,fill:'#16899a',tabindex:0});
                    dot.append(svgNode('title',{},`${row.label}: ${money.format(row.expenses)}`));svg.append(dot);
                } else {
                    const barW = Math.min(28,step*.32);
                    for (const [key,offset,color] of [['income',-barW,'#16899a'],['expenses',1,'#dc5666']]) {
                        const bar = svgNode('rect',{x:x(i)+offset,y:y(row[key]),width:barW-1,height:Math.max(0,top+plotH-y(row[key])),fill:color,rx:2,tabindex:0});
                        bar.append(svgNode('title',{},`${row.label} ${key}: ${money.format(row[key])}`));svg.append(bar);
                    }
                }
            });
            svg.style.minWidth = `${width}px`;
            host.append(svg);
            if (width > host.clientWidth + 1) {
                const hint = document.createElement('p');
                hint.className = 'text-muted small mb-0';hint.textContent = 'Scroll sideways to see all months.';
                host.append(hint);
            }
        }
    }
    function validate() {
        to.setCustomValidity('');
        if (from.value && to.value) {
            const [fy,fm] = from.value.split('-').map(Number), [ty,tm] = to.value.split('-').map(Number);
            const span = (ty-fy)*12+tm-fm;
            if (span < 0) to.setCustomValidity('The end month must be on or after the start month.');
            else if (span >= 12) to.setCustomValidity('Choose a period of up to 12 months.');
        }
        return form.reportValidity();
    }
    async function load() {
        if (request) request.abort();
        const mine = ++version;
        results.setAttribute('aria-busy','false');loading.classList.add('d-none');error.classList.add('d-none');
        if (!validate()) return;
        const controller = new AbortController();request = controller;
        const url = new URL(form.action,window.location.href);
        url.searchParams.set('from',from.value);url.searchParams.set('to',to.value);
        results.setAttribute('aria-busy','true');loading.classList.remove('d-none');
        try {
            const response = await fetch(url,{headers:{'X-Requested-With':'XMLHttpRequest'},credentials:'same-origin',cache:'no-store',signal:controller.signal});
            if (!response.ok || response.redirected) throw new Error('Analytics request failed');
            const html = await response.text();if (mine !== version) return;
            results.innerHTML = html;
            const overview = results.querySelector('.analytics-overview');
            if (overview) {
                from.value = overview.dataset.from;to.value = overview.dataset.to;
                url.searchParams.set('from',from.value);url.searchParams.set('to',to.value);
            }
            window.history.replaceState(null,'',url.toString());drawCharts();
        } catch (failure) {
            if (failure.name !== 'AbortError' && mine === version) error.classList.remove('d-none');
        } finally {
            if (mine === version) {loading.classList.add('d-none');results.setAttribute('aria-busy','false');request=null;}
        }
    }
    form.addEventListener('submit',event=>{event.preventDefault();load();});
    from.addEventListener('change',load);to.addEventListener('change',load);
    document.getElementById('analyticsRetry').addEventListener('click',load);
    results.addEventListener('click',event=>{if(event.target.closest('[data-print-analytics]'))window.print();});
    // Back/forward restores both period controls and the displayed dataset.
    window.addEventListener('popstate',()=>{
        const url=new URL(window.location.href);
        if(url.searchParams.has('from')&&url.searchParams.has('to')){from.value=url.searchParams.get('from');to.value=url.searchParams.get('to');load();}
        else window.location.reload();
    });
    let resizeTimer;
    window.addEventListener('resize',()=>{clearTimeout(resizeTimer);resizeTimer=setTimeout(drawCharts,150);});
    drawCharts();
})();
