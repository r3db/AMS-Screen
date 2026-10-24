function updateSpool(spool, color, type, weight) {
	const e = document.getElementById(`spool-${spool}`);

	if(!e) {
		return;
	}

	e.querySelector('.spool-color').style.fill   = color;
	e.querySelector('.spool-type').textContent   = type == null ? "?" : type;
	e.querySelector('.spool-weight').textContent = weight == null ? "?" : weight + "g";

	showSVGElement(e);
}

function hideSpool(spool) {
	hideSVGElement(document.getElementById(`spool-${spool}`));
}

function showConnector(spool) {
	showSVGElement(document.getElementById(`spool-connector-${spool}`));
}

function hideConnectors() {
	for (let i = 0; i < 4; ++i) {
		hideSVGElement(document.getElementById(`spool-connector-${i}`));
	}
}

function markSpoolAsActive(spool) {
	for (let i = 0; i < 4; ++i) {
		const sg = document.getElementById(`spool-${i}`);
		
		if(sg) {
			sg.querySelector('.spool-connector').style.display = (spool == i ? 'none' : '');
		}
    }
}

function hideSVGElement(e) {
	if(e) {
		e.style.display = 'none';
	}
}

function showSVGElement(e) {
	if(e) {
		e.style.display = '';
	}
}

function hideConnectingMessage() {
	document.querySelector('.container .item.message').style.display = 'none';
}

function hideUnits() {
	document.querySelector('.container .item.units').style.display = 'none';
}

function showConnectingMessage() {
	document.querySelector('.container .item.message').style.display = 'flex';
}

function showUnits() {
	document.querySelector('.container .item.units').style.display = 'block';
}

function setTemperature(value) {
	const e = document.getElementById(`temperature`);

	if(!e) {
		return;
	}

	e.textContent = value + "ºC"
}

function setHumidity(value) {
	const e = document.getElementById(`humidity`);

	if(!e) {
		return;
	}

	e.textContent = value + "%"
}


function setupSignalR() {
	const connection = new signalR.HubConnectionBuilder()
		.withUrl("/printerHub")
	    .withAutomaticReconnect()
	    .build();

	connection.on("AMS-Units", function (units) {
		hideConnectingMessage();
		showUnits();

		for (var i = 0; i < units.length; i++) {
			let unit = units[i];

			if (unit.index != 0) {
				continue;
			}

			setTemperature(unit.temperature);
			setHumidity(unit.humidity);

			for (var k = 0; k < unit.trays.length; k++) {
				let tray = unit.trays[k];
				updateSpool(tray.index, tray.color, tray.type, tray.weight);

				if (tray.isActive) {
					hideConnectors();
					showConnector(tray.index);
				}
			}
		}
	});

	connection.on("System-Disconnected", function() {
		showConnectingMessage();
		hideUnits();
	})


    connection.start().then(function () {
        console.log("Connected to Bambu MQTT!");
    }).catch(function (err) {
        return console.error(err.toString());
    });
}

hideConnectors();
hideSpool(0);
hideSpool(1);
hideSpool(2);
hideSpool(3);

setupSignalR();