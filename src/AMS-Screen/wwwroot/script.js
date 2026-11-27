// ---------------------------------------------------------------------------
// Layout of each device inside its drawing (SVG units).
// Every device is one page of the carousel. Spools are listed left to right.
// ---------------------------------------------------------------------------
const SLOT_X = {
	ams: [0, 160, 320, 480],
	ht:  [220],
	ext: [313],
};

const INK = '#f4f4f4';

let devices   = [];       // devices as drawn: { name, kind, t, h, dry, slots }
let drawn     = '';       // what is currently on screen, used to skip identical redraws
let pageIndex = 0;        // page the carousel is on
let activeKey = null;     // device that currently feeds the toolhead
let dragging  = false;
let queued    = null;     // units received while the user was dragging

// ---------------------------------------------------------------------------
// Data
// ---------------------------------------------------------------------------
function toDevices(units) {
	return units.map(unit => {
		const kind  = SLOT_X[unit.kind] ? unit.kind : 'ams';
		const slots = SLOT_X[kind].map(() => null);

		for (const tray of unit.trays || []) {
			if (tray.index < 0 || tray.index >= slots.length || tray.isEmpty) {
				continue;
			}

			slots[tray.index] = {
				type:   tray.type,
				weight: tray.weight,
				color:  /^#[0-9a-f]{6}$/i.test(tray.color || '') ? tray.color : '#ffffff',
				active: !!tray.isActive,
			};
		}

		return {
			name: unit.name || 'AMS',
			kind,
			t:    unit.temperature,
			h:    unit.humidity,
			dry:  unit.dryTimeMinutes,
			slots,
		};
	});
}

function findActive(list) {
	for (let d = 0; d < list.length; d++) {
		const s = list[d].slots.findIndex(slot => slot && slot.active);

		if (s >= 0) {
			return { d, s, color: list[d].slots[s].color };
		}
	}

	return null;
}

// ---------------------------------------------------------------------------
// Drawing
// ---------------------------------------------------------------------------
const esc = text => String(text).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

function formatMinutes(m) {
	return m >= 60 ? `${Math.floor(m / 60)}h${String(m % 60).padStart(2, '0')}m` : `${m}m`;
}

function formatTemperature(t) {
	return Number.isFinite(t) ? `${+t.toFixed(1)}°C` : '–';
}

function formatHumidity(h) {
	return Number.isFinite(h) ? `${Math.round(h)}%` : '–';
}

function formatWeight(w) {
	return Number.isFinite(w) ? `${Math.round(w)}g` : '?';
}

function spoolSvg(slot, x, hideFrontFlange, showHub) {
	if (!slot) {
		return '';
	}

	const cy = 400, r = 144, labelX = x + 231;

	return `
		<ellipse cx="${x + 168}" cy="${cy}" rx="50" ry="175" fill="#2b1e18" />
		<ellipse cx="${x + 187}" cy="${cy}" rx="44" ry="${r}" fill="${slot.color}" />
		<rect x="${x + 187}" y="${cy - r}" width="82" height="${2 * r}" fill="${slot.color}" />
		${hideFrontFlange ? '' : `<ellipse cx="${x + 292}" cy="${cy}" rx="50" ry="175" fill="#3b3b3b" />`}
		${showHub ? `<ellipse cx="${x + 292}" cy="${cy}" rx="16" ry="40" fill="url(#SpoolHole)" />` : ''}
		<text class="spool-type" x="${labelX}" y="52">${esc(slot.type == null ? '?' : slot.type)}</text>
		<text class="spool-weight" x="${labelX}" y="92">${formatWeight(slot.weight)}</text>
		<line x1="${labelX}" y1="114" x2="${labelX}" y2="${cy - r}" stroke="${INK}" stroke-width="1.5" stroke-dasharray="5 5" />`;
}

function dryingSvg(minutes) {
	const rays = [0, 45, 90, 135, 180, 225, 270, 315]
		.map(a => `<line y1="-14" y2="-20" transform="rotate(${a})" />`)
		.join('');

	return `
		<line x1="28" x2="92" y1="256" y2="256" stroke="#333" />
		<g transform="translate(60 294)" stroke="${INK}" stroke-width="3" stroke-linecap="round" fill="none">
			<circle r="9" />${rays}
		</g>
		<text class="reading small" x="60" y="346">${formatMinutes(minutes)}</text>`;
}

function readingsSvg(device, x) {
	const drying = Number.isFinite(device.dry) && device.dry > 0;

	return `
		<g transform="translate(${x} 10)">
			<rect width="120" height="${drying ? 382 : 272}" rx="60" fill="#0b0b0b" stroke="#5a5a5a" stroke-width="2" />
			<path transform="translate(60 58)" d="M0-22C10-8 17 0 17 9A17 17 0 0 1-17 9C-17 0-10-8 0-22Z" fill="${INK}" />
			<text class="reading" x="60" y="118">${formatHumidity(device.h)}</text>
			<line x1="28" x2="92" y1="138" y2="138" stroke="#333" />
			<g transform="translate(58 172)" fill="none" stroke="${INK}" stroke-width="3" stroke-linecap="round" stroke-linejoin="round">
				<path d="M-7-19a7 7 0 0 1 14 0V3a13 13 0 1 1-14 0Z" stroke-width="2.5" />
				<rect x="-2.5" y="-6" width="5" height="20" rx="2.5" fill="${INK}" stroke="none" />
				<circle cy="14" r="7" fill="${INK}" stroke="none" />
				<path d="M12-14h5M12-8h5M12-2h5" stroke-width="2" />
			</g>
			<text class="reading" x="60" y="236">${formatTemperature(device.t)}</text>
			${drying ? dryingSvg(device.dry) : ''}
		</g>`;
}

function toolheadSvg(x, ringColor) {
	return `
		<g transform="translate(${x} 662) scale(.25) translate(-500 -190)">
			<path d="M328 660H672V745L668 795H588V742H578V790H503V742H413V795H334L328 745Z" fill="#1c1c1c" />
			<rect x="365" y="668" width="271" height="74" fill="#101010" />
			<path d="M330 668V232Q330 190 372 190H386L392 207H608L614 190H628Q670 190 670 232V668Z" fill="#d6d6d6" />
			<path d="M366 668V330Q366 272 424 272H576Q634 272 634 330V668Z" fill="#e4e4e4" />
			<text x="500" y="336" text-anchor="middle" font-family="Arial,Helvetica,sans-serif" font-size="44" font-weight="700" fill="#3a3a3a">Bambu Lab</text>
			<circle cx="500" cy="500" r="112" fill="#efefef" stroke="${ringColor}" stroke-width="9" />
			<circle cx="500" cy="500" r="94" fill="#141414" />
			<circle cx="500" cy="500" r="89" fill="none" stroke="#2c2c2c" stroke-width="9" stroke-dasharray="3 5" />
			<circle cx="500" cy="500" r="68" fill="#0b0b0b" />
			<circle cx="500" cy="500" r="6" fill="none" stroke="#2c2c2c" stroke-width="2" />
		</g>`;
}

function pageSvg(device, ringColor) {
	const offsets = SLOT_X[device.kind];
	const first   = offsets[0];
	const last    = offsets[offsets.length - 1];
	const housing = device.kind !== 'ext';
	const width   = last - first + 302;
	const fadeW   = Math.min(258, width * 0.55);
	let svg = '';

	// Spools. In a housing the last spool is covered by the end cap, which doubles as its front flange.
	// Without a neighbour in front, a spool gets its own hub.
	device.slots.forEach((slot, k) => {
		const isLast = housing && k === offsets.length - 1;
		const hasNeighbour = !!device.slots[k + 1];
		svg += spoolSvg(slot, offsets[k], isLast, !isLast && !hasNeighbour);
	});

	if (housing) {
		svg += `
			<ellipse cx="${last + 305}" cy="400" rx="62" ry="175" fill="#3b3b3b" />
			<ellipse cx="${last + 305}" cy="400" rx="16" ry="40" fill="url(#SpoolHole)" />
			<path d="M${first + 108} 400C${first + 108} 300 ${first + 130} 200 ${first + 180} 195H${last + 300}M${last + 212} 400A88 205 0 0 1 ${last + 388} 400" fill="none" stroke="#7a7a7a" stroke-width="1.5" />
			<rect x="${first + 97}" y="400" width="${width}" height="206" fill="#2a2a2a" fill-opacity=".45" stroke="#777" stroke-width="1.5" />
			<rect x="${first + 97 + width - fadeW}" y="401" width="${fadeW - 1}" height="204" fill="url(#UnitFade)" />`
			+ readingsSvg(device, device.kind === 'ams' ? 900 : last + 429);
	}

	// Every slot, empty or not, is connected to the toolhead.
	const headX    = (first + last) / 2 + 231;
	const lineBase = housing ? 606 : 578;

	svg += offsets
		.map(x => `<path d="M${x + 231} ${lineBase}V640H${headX}V666" fill="none" stroke="${INK}" stroke-width="1.5" />`)
		.join('');

	svg += toolheadSvg(headX, ringColor);

	return `<svg viewBox="0 -56 1087 881" role="group" aria-label="${esc(device.name)}">
		<text class="unit-name" x="543" y="-14">${esc(device.name)}</text>${svg}</svg>`;
}

// ---------------------------------------------------------------------------
// Carousel
// ---------------------------------------------------------------------------
const carousel = document.querySelector('.container .item.units');

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function goTo(index, instant) {
	pageIndex = Math.max(0, Math.min(devices.length - 1, index));
	carousel.scrollTo({ left: pageIndex * carousel.clientWidth, behavior: instant || reducedMotion() ? 'auto' : 'smooth' });
}

function render(list) {
	const active = findActive(list);
	const key    = JSON.stringify([list, active && [active.d, active.s]]);

	if (key === drawn) {
		return;
	}

	drawn   = key;
	devices = list;

	const ring = active ? active.color : '#6b6b6b';

	carousel.innerHTML = devices
		.map(d => `<div class="page">${pageSvg(d, ring)}</div>`)
		.join('');

	// Keep the page the user is looking at, even though the pages were rebuilt.
	pageIndex = Math.max(0, Math.min(devices.length - 1, pageIndex));
	carousel.scrollLeft = pageIndex * carousel.clientWidth;

	// When the toolhead switches to a spool on another device, show that device.
	// A moment with no active spool (e.g. between filament changes) does not count as a switch.
	if (active) {
		if (active.d !== activeKey) {
			goTo(active.d, activeKey === null);
		}

		activeKey = active.d;
	}
}

function applyUnits(units) {
	if (dragging) {
		queued = units;
		return;
	}

	render(toDevices(units));
}

carousel.addEventListener('scroll', () => {
	if (!dragging && carousel.clientWidth > 0) {
		pageIndex = Math.round(carousel.scrollLeft / carousel.clientWidth);
	}
});

window.addEventListener('resize', () => {
	carousel.scrollLeft = pageIndex * carousel.clientWidth;
});

// Mouse dragging (touch scrolling already works natively).
let dragStart = null;

carousel.addEventListener('pointerdown', e => {
	if (e.pointerType !== 'mouse') {
		return;
	}

	dragging  = true;
	dragStart = { x: e.clientX, left: carousel.scrollLeft };
	carousel.style.scrollSnapType = 'none';
	carousel.setPointerCapture(e.pointerId);
	carousel.classList.add('dragging');
});

carousel.addEventListener('pointermove', e => {
	if (dragging) {
		carousel.scrollLeft = dragStart.left - (e.clientX - dragStart.x);
	}
});

function endDrag(e) {
	if (!dragging) {
		return;
	}

	const width = carousel.clientWidth;
	const moved = e.clientX - dragStart.x;
	let index   = Math.round(dragStart.left / width);

	if (Math.abs(moved) > width * 0.15) {
		index += moved < 0 ? 1 : -1;
	}

	dragging = false;
	carousel.classList.remove('dragging');
	carousel.style.scrollSnapType = '';

	if (queued) {
		const units = queued;
		queued = null;
		render(toDevices(units));
	}

	goTo(index);
}

carousel.addEventListener('pointerup', endDrag);
carousel.addEventListener('pointercancel', endDrag);

// ---------------------------------------------------------------------------
// Connection state
// ---------------------------------------------------------------------------
function hideConnectingMessage() {
	document.querySelector('.container .item.message').style.display = 'none';
}

function showConnectingMessage() {
	document.querySelector('.container .item.message').style.display = 'flex';
}

function hideUnits() {
	carousel.style.display = 'none';
}

function showUnits() {
	carousel.style.display = 'flex';
}

function setupSignalR() {
	const connection = new signalR.HubConnectionBuilder()
		.withUrl("/printerHub")
	    .withAutomaticReconnect()
	    .build();

	connection.on("AMS-Units", function (units) {
		hideConnectingMessage();
		showUnits();
		applyUnits(units);
	});

	connection.on("System-Disconnected", function() {
		showConnectingMessage();
		hideUnits();

		// Start fresh once the printer comes back.
		drawn     = '';
		activeKey = null;
		pageIndex = 0;
	})


    connection.start().then(function () {
        console.log("Connected to Bambu MQTT!");
    }).catch(function (err) {
        return console.error(err.toString());
    });
}

setupSignalR();
