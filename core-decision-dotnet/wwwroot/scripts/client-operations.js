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

async function update_setting(query_id, result_id, type) {
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
            body: JSON.stringify({ type, query: query_element.value })
        }
        const response = await fetch('/job/setting', data);
        if (result_element) {
            let result = '';
            if (type === 'Q') {
                const result_obj = await response.json();
                result = JSON.stringify(result_obj, (key, value) => {
                    if (key !== "Settings") return value;
                    if (typeof value !== "string") return value;
                    return JSON.parse(value);
                }, "\t");
            } else {
                result = await response.text();
            }
            result_element.innerHTML = result;
        }

    } catch (e) {
        if (result_element) result_element.innerHTML = e.message;
    }
}