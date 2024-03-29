function select_job(query) {
    
    document.querySelectorAll('tr.Selected').forEach(function (item) {
        item.classList.remove('Selected');
        item.previousElementSibling?.classList.remove('Selected-pr');
    });

    if (!query) return;

    document.querySelectorAll('tr' + query).forEach(function (item) {
        item.classList.add('Selected');
        item.previousElementSibling?.classList.add('Selected-pr');
    });
}

async function update_setting(query_id, result_id) {
    const result_element = document.getElementById(result_id);
    
    try {
        const query_element = document.getElementById(query_id);
        if (!query_element) return;

        const data = {
            method: 'POST',
            headers: {
                'Accept': 'plain/text',
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(query_element.value)
        }
        let response = await fetch('/job/setting', data);
        if (result_element) result_element.innerHTML = await response.text();

    } catch (e) {
        if (result_element) result_element.innerHTML = e.message;
    }
}