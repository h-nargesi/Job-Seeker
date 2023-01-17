function select_job(query) {
    
    document.querySelectorAll('tr.Selected').forEach(function (item) {
        item.classList.remove('Selected');
        item.previousElementSibling.classList.remove('Selected-pr');
    });

    if (!query) return;

    document.querySelectorAll('tr' + query).forEach(function (item) {
        item.classList.add('Selected');
        item.previousElementSibling.classList.add('Selected-pr');
    });
}