// ApexGantt interop for the Project schedule screen (Pages/Projects.razor).
// ApexGantt has no Blazor wrapper: it is loaded from the CDN (apexgantt.min.js + the apexgantt.ignite-ui add-on,
// which makes the chart follow the page's Ignite UI theme tokens) and driven from .NET through this module.
//
// Design system mapping (ApexCharts tokens):
//   leaf track  gantt-track-blue  #a6d8fd, done segment chart-1 #008ffb (solid)
//   done track  gantt-track-green #a6f5da, done segment color-success #00a86f
//   summary     navy #0a2540 (25% track, solid done), milestones ink #1a1a2e (green when complete)
//   today line  chart-4 #ff4560, gridlines sample-grid-border, header sample-grid-header.
window.ganttInterop = (() => {
    const charts = new Map(); // element -> { gantt, dotnet, meta, observer, onClick, onProgress, ppd, selectedId }

    const COLORS = {
        track: '#a6d8fd', done: '#008ffb',
        trackComplete: '#a6f5da', doneComplete: '#00a86f',
        summaryTrack: '#c2c9cf', summaryDone: '#0a2540',
        milestone: '#1a1a2e', milestoneDone: '#00a86f',
        today: '#ff4560', arrow: '#4b5563', grid: '#eef0f3',
        header: '#f8f9fb', ink: '#1f2937', selected: '#eff6ff'
    };

    const isoDate = (s) => (s || '').slice(0, 10);
    const shortDate = (s) => {
        const d = new Date(isoDate(s) + 'T00:00:00');
        return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    };
    const esc = (s) => (window.ApexGantt && window.ApexGantt.escapeHtml ? window.ApexGantt.escapeHtml(String(s ?? '')) : String(s ?? ''));

    function buildSeries(payload) {
        const meta = new Map();
        const incoming = new Map();
        for (const d of payload.dependencies) {
            if (!incoming.has(d.successorId)) incoming.set(d.successorId, []);
            incoming.get(d.successorId).push(d);
        }
        const extraLinks = [];
        const series = payload.tasks.map(t => {
            meta.set(String(t.id), t);
            const deps = incoming.get(t.id) || [];
            const first = deps[0];
            deps.slice(1).forEach(d => extraLinks.push(d));
            const complete = t.percentComplete >= 100;
            const task = {
                id: String(t.id),
                name: t.name,
                startTime: isoDate(t.start),
                progress: t.percentComplete,
                parentId: t.parentId != null ? String(t.parentId) : undefined,
            };
            if (t.isMilestone) {
                task.type = 'milestone';
                task.barBackgroundColor = complete ? COLORS.milestoneDone : COLORS.milestone;
            } else {
                task.endTime = isoDate(t.finish);
                if (!t.isSummary) task.barBackgroundColor = complete ? COLORS.trackComplete : COLORS.track;
            }
            if (first) task.dependency = { taskId: String(first.predecessorId), type: first.typeCode, lag: first.lagDays };
            if (t.assignments && t.assignments.length) {
                task.assignees = t.assignments.map(a => ({ name: a.resourceName }));
            }
            return task;
        });
        return { series, meta, extraLinks };
    }

    function options(payload, state, built) {
        const ApexGantt = window.ApexGantt;
        const K = ApexGantt.ColumnKey;
        const meta = built.meta;
        const today = state.today;
        return {
            series: built.series,
            inputDateFormat: 'YYYY-MM-DD',
            sortBy: [],                          // keep the server's WBS order
            rowHeight: 40,
            pixelsPerDay: state.ppd,
            tasksContainerWidth: 520,
            autoSizeColumns: true,
            columnLines: true,
            enableSelection: true,
            showCheckboxColumn: false,
            enableTaskDrag: false,               // dates are owned by the server scheduler
            enableTaskResize: false,
            enableProgressDrag: true,            // persisted via PATCH /api/tasks/{id}/progress
            enableTaskEdit: false,
            enableScrollButtons: false,
            theme: 'light',
            fontFamily: 'Inter, -apple-system, "Segoe UI", sans-serif',
            fontSize: '13px',
            fontColor: COLORS.ink,
            headerBackground: COLORS.header,
            backgroundColor: '#ffffff',
            rowBackgroundColors: ['#ffffff'],
            cellBorderColor: COLORS.grid,
            arrowColor: COLORS.arrow,
            barBackgroundColor: COLORS.track,
            barBorderRadius: '4px',
            summaryBarColor: COLORS.summaryTrack,
            milestoneColor: COLORS.milestone,
            barLabel: { position: 'right', render: (task) => esc(labelFor(meta.get(task.id))) },
            locale: { displayDateFormat: 'MMM D' },
            annotations: [{
                x1: today,
                borderColor: COLORS.today,
                borderWidth: 2,
                label: { text: 'Today · ' + shortDate(today), fontColor: COLORS.today, fontWeight: '600', fontSize: '11px' }
            }],
            columnConfig: [
                { key: 'wbs', title: 'WBS', minWidth: '44px', maxWidth: '56px', flexGrow: 0.5, render: (ctx) => `<span class="gx-wbs">${esc(meta.get(ctx.task.id)?.wbs)}</span>` },
                { key: K.Name, title: 'Task', minWidth: '180px', maxWidth: '260px', flexGrow: 3 },
                { key: 'dur', title: 'Dur.', minWidth: '52px', maxWidth: '64px', flexGrow: 0.8, render: (ctx) => { const m = meta.get(ctx.task.id); return m ? `${m.isMilestone ? 0 : Math.round(m.durationDays)} d` : ''; } },
                { key: K.StartTime, title: 'Start', minWidth: '60px', maxWidth: '72px', flexGrow: 1 },
                { key: K.EndTime, title: 'Finish', minWidth: '60px', maxWidth: '72px', flexGrow: 1 },
                { key: 'pct', title: '%', minWidth: '44px', maxWidth: '52px', flexGrow: 0.6, render: (ctx) => { const m = meta.get(ctx.task.id); return m ? `<span class="gx-pct">${m.percentComplete}%</span>` : ''; } },
            ],
            tooltipTemplate: (task) => {
                const m = meta.get(task.id);
                if (!m) return esc(task.name);
                const dates = m.isMilestone ? shortDate(m.start) : `${shortDate(m.start)} – ${shortDate(m.finish)}`;
                const who = m.assignments && m.assignments.length ? `<div class="gx-tip-who">${esc(m.assignments.map(a => a.resourceName).join(', '))}</div>` : '';
                return `<div class="gx-tip"><div class="gx-tip-title">${esc(m.wbs)} · ${esc(m.name)}</div><div class="gx-tip-row">${esc(dates)} · ${m.percentComplete}% complete</div>${who}</div>`;
            },
        };
    }

    function labelFor(m) {
        if (!m) return '';
        if (m.isMilestone) return shortDate(m.start);
        if (m.isSummary) return '';
        return (m.assignments || []).map(a => a.resourceName).join(', ');
    }

    // ApexGantt shades the done segment from the track colour; the design wants solid brand segments,
    // so repaint them (and the selected-row wash) after every render. Bars carry data-taskid.
    function paint(el) {
        const c = charts.get(el);
        if (!c) return;
        el.querySelectorAll('.bar-timeline-progress').forEach(p => {
            const host = p.closest('[data-taskid]');
            const m = host && c.meta.get(host.getAttribute('data-taskid'));
            if (!m) return;
            const color = m.isSummary ? COLORS.summaryDone : (m.percentComplete >= 100 ? COLORS.doneComplete : COLORS.done);
            if (p.style.backgroundColor !== color) p.style.backgroundColor = color;
        });
        const sel = c.selectedId != null ? String(c.selectedId) : null;
        el.querySelectorAll('.tasks-data-row[data-taskid], .timeline-data-row[data-taskid]').forEach(r => {
            r.classList.toggle('gx-selected', r.getAttribute('data-taskid') === sel);
        });
    }

    function attach(el, dotnet) {
        const onClick = (e) => {
            const host = e.target.closest('[data-taskid]');
            if (!host || !el.contains(host)) return;
            const id = host.getAttribute('data-taskid');
            const c = charts.get(el);
            if (!c || !c.meta.has(id)) return;
            c.selectedId = Number(id);
            try { c.gantt.setSelectedTasks([id]); } catch { /* selection is cosmetic */ }
            paint(el);
            dotnet.invokeMethodAsync('OnGanttTaskSelected', Number(id));
        };
        const onProgress = (e) => {
            const d = e.detail || {};
            if (d.taskId == null) return;
            dotnet.invokeMethodAsync('OnGanttProgressDragged', Number(d.taskId), Math.round(d.newProgress));
        };
        el.addEventListener('click', onClick);
        const progressEvent = (window.ApexGantt.GanttEvents && window.ApexGantt.GanttEvents.TASK_PROGRESS_CHANGED) || 'taskProgressChanged';
        el.addEventListener(progressEvent, onProgress);
        let scheduled = false;
        const observer = new MutationObserver(() => {
            if (scheduled) return;
            scheduled = true;
            requestAnimationFrame(() => { scheduled = false; paint(el); });
        });
        observer.observe(el, { childList: true, subtree: true });
        return { onClick, onProgress, progressEvent, observer };
    }

    function build(el, payload, state) {
        const c = charts.get(el);
        const built = buildSeries(payload);
        const scroller = el.querySelector('.timeline-body-wrapper');
        const scrollLeft = scroller ? scroller.scrollLeft : null;
        const gantt = new window.ApexGantt(el, options(payload, state, built)); // replaces any live chart on el
        gantt.render();
        for (const d of built.extraLinks) {
            try {
                const ok = gantt.canAddDependency ? gantt.canAddDependency(String(d.predecessorId), String(d.successorId)) : { ok: true };
                if (ok.ok) gantt.addDependency(String(d.predecessorId), String(d.successorId), { type: d.typeCode, lag: d.lagDays });
            } catch { /* a link the chart can't draw (e.g. summary-descendant) is still listed in the details panel */ }
        }
        try { gantt.clearHistory && gantt.clearHistory(); } catch { }
        c.gantt = gantt;
        c.meta = built.meta;
        if (c.selectedId != null) { try { gantt.setSelectedTasks([String(c.selectedId)]); } catch { } }
        const newScroller = el.querySelector('.timeline-body-wrapper');
        if (newScroller && scrollLeft != null) newScroller.scrollLeft = scrollLeft;
        paint(el);
    }

    return {
        /** Create the chart. state: { ppd, today: 'YYYY-MM-DD', selectedId } */
        render(el, dotnet, payload, state) {
            if (!window.ApexGantt) throw new Error('ApexGantt script not loaded');
            this.destroy(el);
            const handlers = attach(el, dotnet);
            charts.set(el, { gantt: null, dotnet, meta: new Map(), ppd: state.ppd, today: state.today, selectedId: state.selectedId, ...handlers });
            build(el, payload, state);
            if (state.scrollToToday) this.scrollToToday(el);
        },
        /** Replace the data (every task mutation returns a fresh gantt payload). */
        update(el, payload, state) {
            const c = charts.get(el);
            if (!c) return;
            c.ppd = state.ppd; c.today = state.today; c.selectedId = state.selectedId;
            build(el, payload, state);
        },
        setZoom(el, ppd) {
            const c = charts.get(el);
            if (!c || !c.gantt) return;
            c.ppd = ppd;
            c.gantt.update({ pixelsPerDay: ppd });
            paint(el);
        },
        select(el, id) {
            const c = charts.get(el);
            if (!c || !c.gantt) return;
            c.selectedId = id;
            try { c.gantt.setSelectedTasks(id == null ? [] : [String(id)]); } catch { }
            try { if (id != null) c.gantt.scrollToTask(String(id)); } catch { }
            paint(el);
        },
        scrollToToday(el) {
            const scroller = el.querySelector('.timeline-body-wrapper');
            const line = el.querySelector('.annotation');
            if (!scroller || !line) return;
            const offset = line.getBoundingClientRect().left - scroller.getBoundingClientRect().left + scroller.scrollLeft;
            scroller.scrollLeft = Math.max(0, offset - scroller.clientWidth / 3);
        },
        destroy(el) {
            const c = charts.get(el);
            if (!c) return;
            el.removeEventListener('click', c.onClick);
            el.removeEventListener(c.progressEvent, c.onProgress);
            c.observer.disconnect();
            try { c.gantt && !c.gantt.isDestroyed() && c.gantt.destroy(); } catch { }
            charts.delete(el);
        }
    };
})();
