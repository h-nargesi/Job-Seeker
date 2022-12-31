(function () {
  const reader = new stmd.DocParser();
  const writer = new stmd.HtmlRenderer();

  function display(element) {
    element = document.getElementById(element);

    const parsed = reader.parse(element.innerHTML);
    const content = writer.renderBlock(parsed);

    element.innerHTML = content;
  }

  const buttons = document.querySelectorAll('button[data-bs-toggle="modal"]');
  buttons.forEach(function (but) {
    but.addEventListener("click", function () {
      let id = but.getAttribute('data-id');
      console.log("Model", id);
      display('logs-body-' + id);
    }, false);
  });

})();

