const userSelect = document.getElementById("userSelect");

fetch("http://localhost:5000/api/admin/users")
	.then((res) => res.json())
	.then((users) => {
		users.forEach((u) => {
			const option = document.createElement("option");
			option.value = u.uid;
			option.textContent = u.uid;
			userSelect.appendChild(option);
		});

		if (users.length > 0) {
			loadUser(users[0].uid);
		}
	});

userSelect.addEventListener("change", (e) => {
	loadUser(e.target.value);
});

function loadUser(uid) {
	fetch(`http://localhost:5000/api/admin/user/${uid}`)
		.then((res) => res.json())
		.then((data) => {
			console.log("RAW USER DATA:", data);
			console.log("DRAWINGS:", data.drawings);
			console.log("TOPICS:", data.topics);

			drawDurationChart(data.drawings);
			drawEditChart(data.drawings);
			drawReferenceChart(data.topics);

			fetch(`http://localhost:5000/api/admin/user/${uid}/colors`)
				.then((res) => res.json())
				.then((colorData) => {
					drawColorChart(colorData);
				});

			fetch(`http://localhost:5000/api/admin/user/${uid}/strokes-per-topic`)
				.then((res) => res.json())
				.then((strokeData) => {
					drawStrokeCountChart(strokeData);
				});

			fetch(`http://localhost:5000/api/admin/user/${uid}/bounds`)
				.then((res) => res.json())
				.then((boundsData) => {
					drawBoundsChart(boundsData);
				});
		});
}

//chart 1: average duration per drawing
let durationChart;

function drawDurationChart(drawings) {
	const labels = drawings.map((_, i) => `Drawing ${i + 1}`);
	const values = drawings.map((d) => d.total_duration);

	if (durationChart) durationChart.destroy();

	durationChart = new Chart(document.getElementById("durationChart"), {
		type: "line",
		data: {
			labels,
			datasets: [
				{
					label: "Duration (s)",
					data: values,
				},
			],
		},
	});
}

//chart 2: erase & undo behavior
let editChart;

function drawEditChart(drawings) {
	const labels = drawings.map((_, i) => `Drawing ${i + 1}`);

	const eraseCounts = drawings.map((d) => d.erase_count);
	const undoCounts = drawings.map((d) => d.undo_count);

	if (editChart) editChart.destroy();

	editChart = new Chart(document.getElementById("editChart"), {
		type: "bar",
		data: {
			labels,
			datasets: [
				{ label: "Erases", data: eraseCounts },
				{ label: "Undos", data: undoCounts },
			],
		},
	});
}

//chart 3: reference usage
let referenceChart;

function drawReferenceChart(topics) {
	const withRef = topics.filter((t) => t.used_reference).length;
	const withoutRef = topics.length - withRef;

	if (referenceChart) referenceChart.destroy();

	referenceChart = new Chart(document.getElementById("referenceChart"), {
		type: "pie",
		data: {
			labels: ["With Reference", "Without Reference"],
			datasets: [
				{
					data: [withRef, withoutRef],
				},
			],
		},
	});
}

//chart 4: color changes & which color used
function drawColorChart(data) {
	new Chart(document.getElementById("colorChart"), {
		type: "pie",
		data: {
			labels: data.map((d) => d.color),
			datasets: [
				{
					data: data.map((d) => Number(d.count)),
				},
			],
		},
	});
}

//chart 5: how many strokes drawn
function drawStrokeCountChart(data) {
	new Chart(document.getElementById("strokeCountChart"), {
		type: "bar",
		data: {
			labels: data.map((d) => d.topic),
			datasets: [
				{
					label: "Strokes per topic",
					data: data.map((d) => Number(d.stroke_count)),
				},
			],
		},
	});
}

//chart 6: bounds -> how "big" drawing
function drawBoundsChart(data) {
	new Chart(document.getElementById("boundsChart"), {
		type: "bar",
		data: {
			labels: data.map((d) => d.topic),
			datasets: [
				{
					label: "Total Drawing Area",
					data: data.map((d) => Number(d.total_area.toFixed(2))),
					backgroundColor: "rgba(54, 162, 235, 0.6)",
				},
			],
		},
		options: {
			scales: {
				y: {
					beginAtZero: true,
					title: {
						display: true,
						text: "Area (units²)",
					},
				},
				x: {
					title: {
						display: true,
						text: "Topic",
					},
				},
			},
		},
	});
}
