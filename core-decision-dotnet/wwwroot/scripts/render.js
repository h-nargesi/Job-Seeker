const reader = new stmd.DocParser();
const writer = new stmd.HtmlRenderer();

function display(element) {
    element = document.getElementById(element);

    const parsed = reader.parse(element.innerHTML);
    const content = writer.renderBlock(parsed);

    element.innerHTML = content;
}

function logs(id) {
    const content = document.getElementById('logs-body-' + id).innerHTML;
    document.getElementById('logs-body').innerHTML = content;
    display('logs-body');
    console.log(document.getElementById('logs-body').innerHTML);
    document.getElementById('show-logs').click();
}
