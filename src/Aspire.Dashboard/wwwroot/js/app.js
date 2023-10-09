
// To avoid Flash of Unstyled Content, the body is hidden by default with
// the before-upgrade CSS class. Here we'll find the first web component
// and wait for it to be upgraded. When it is, we'll remove that class
// from the body. 
const firstUndefinedElement = document.body.querySelector(":not(:defined)");

if (firstUndefinedElement) {
    customElements.whenDefined(firstUndefinedElement.localName).then(() => {
        document.body.classList.remove("before-upgrade");
    });
}

window.scrollToEndInTextArea = function (classSelector) {
    let fluentTextAreas = document.querySelectorAll(classSelector);
    if (fluentTextAreas && fluentTextAreas.length > 0) {
        for (const fluentTextArea of fluentTextAreas) {
            if (fluentTextArea && fluentTextArea.shadowRoot) {
                let textArea = fluentTextArea.shadowRoot.querySelector('textarea');
                textArea.scrollTop = textArea.scrollHeight;
            }
        }
    }
};

let isScrolledToContent = false;

window.resetContinuousScrollPosition = function () {
    // Reset to scrolling to the end of the content after switching.
    isScrolledToContent = false;
}

window.initializeContinuousScroll = function () {
    const container = document.querySelector('.continuous-scroll-overflow');
    if (container == null) {
        return;
    }

    // The scroll event is used to detect when the user scrolls to view content.
    container.addEventListener('scroll', () => {
        isScrolledToContent = !isScrolledToBottom(container);
    }, { passive: true });

    // The ResizeObserver reports changes in the grid size.
    // This ensures that the logs are scrolled to the bottom when there are new logs
    // unless the user has scrolled to view content.
    const observer = new ResizeObserver(function () {
        if (!isScrolledToContent) {
            container.scrollTop = container.scrollHeight;
        }
    });
    for (const child of container.children) {
        observer.observe(child);
    }
};

function isScrolledToBottom(container) {
    // Small margin of error. e.g. container is scrolled to within 5px of the bottom.
    const marginOfError = 5;

    return container.scrollHeight - container.clientHeight <= container.scrollTop + marginOfError;
}

window.copyTextToClipboard = function (id, text, precopy, postcopy) {
    let tooltipDiv = document.querySelector('fluent-tooltip[anchor="' + id + '"]').children[0];
    navigator.clipboard.writeText(text)
        .then(() => {
            tooltipDiv.innerText = postcopy;
        })
        .catch(() => {
            tooltipDiv.innerText = 'Could not access clipboard';
        });
    setTimeout(function () { tooltipDiv.innerText = precopy }, 1500);
};

window.updateFluentSelectDisplayValue = function (fluentSelect) {
    if (fluentSelect) {
        fluentSelect.updateDisplayValue();
    }
}

let matched = window.matchMedia('(prefers-color-scheme: dark)').matches;

if (matched) {
    window.DefaultBaseLayerLuminance = 0.08;
} else {
    window.DefaultBaseLayerLuminance = 1.0;
}

function rand() {
    return Math.random();
}

function addMinutes(date, minutes) {
    return new Date(date.getTime() + minutes * 60000);
}

window.initializeGraph = function (id, unit, yValues, xValues, rangeStartTime, rangeEndTime) {
    console.log(`initializeGraph rangeStartTime = ${rangeStartTime}, rangeEndTime = ${rangeEndTime}`);

    var data = [{
        x: xValues, //[time.toISOString()],
        y: yValues, //[rand],
        mode: 'lines',
        line: { color: '#80CAF6' }
    }];

    var olderTime = xValues[0] //addMinutes(time, -1);
    var futureTime = xValues[xValues.length - 1];// addMinutes(time, 1 / 60);

    var layout = {
        margin: { t: 0, r: 0, b: 70, l: 70 },
        xaxis: {
            type: 'date',
            range: [rangeEndTime, rangeStartTime],
            title: {
                text: "Time",
                standoff: 30
            },
            tickformat: "%H:%M:%S"
        },
        yaxis: {
            title: {
                text: unit,
                standoff: 20
            },
            rangemode: "tozero"
        }
    };

    var options = { staticPlot: true };

    Plotly.newPlot(id, data, layout, options);

    //var cnt = 0;

    //var interval = setInterval(function () {

    //    var time = new Date();

    //    var update = {
    //        x: [[time.toISOString()]],
    //        y: [[rand()]]
    //    }

    //    var olderTime = addMinutes(time, -1);
    //    var futureTime = addMinutes(time, 1 / 60);

    //    layout.xaxis.range = [olderTime.toISOString(), futureTime.toISOString()];

    //    Plotly.relayout(id, layout);
    //    Plotly.extendTraces(id, update, [0])

    //    if (++cnt === 100) clearInterval(interval);
    //}, 1000);
};
