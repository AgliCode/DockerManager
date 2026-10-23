export function openLogWindow(text = '', containerName = 'Logi') {
    const windowWidth = 600;
    const windowHeight = 600;
    const gap = 10;

    const windowIndex = window.openWindowIndex ?? 0;
    window.openWindowIndex = windowIndex + 1;

    const left = windowIndex * (windowWidth + gap);

    const logWindow = window.open(
        '',
        '_blank',
        `width=${windowWidth},height=${windowHeight},left=${left},top=50`
    );

    if (!logWindow) {
        return null;
    }

    logWindow.document.title = `Logi - ${containerName}`;

    logWindow.document.body.style.backgroundColor = 'white';
    logWindow.document.body.style.color = 'black';
    logWindow.document.body.style.padding = '16px';

    const heading = logWindow.document.createElement('h2');
    heading.textContent = `Logi kontenera: ${containerName}`;

    const output = logWindow.document.createElement('pre');
    output.textContent = text;

    logWindow.document.body.append(heading, output);

    return logWindow;
}