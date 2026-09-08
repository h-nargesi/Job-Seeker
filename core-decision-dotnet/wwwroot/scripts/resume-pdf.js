function download_resume(jobid) {
    try {
        fetch("/job/resume64?jobid=" + jobid, { method: 'POST' })
            .then(response => response.json())
            .then(result => call_api(result.name, result.content));

    } catch (e) {
        console.error(e);
    }
}

function call_api(name, content) {
    try {
        console.log(name, content);
        fetch("https://api.cloudconvert.com/v2/jobs", {
            method: 'POST',
            headers: {
                'Authorization': API_KEY,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                "tasks": {
                    "import-html": {
                        "operation": "import/base64",
                        "file": content,
                        "filename": name
                    },
                    "task-convert": {
                        "operation": "convert",
                        "input_format": "html",
                        "output_format": "pdf",
                        "engine": "chrome",
                        "engine_version": "112",
                        "input": ["import-html"],
                        "zoom": 1,
                        "page_width": 21,
                        "page_height": 30,
                        "print_background": true,
                        "display_header_footer": false,
                        "wait_until": "load",
                        "wait_time": 1000,
                        "filename": name
                    },
                    "export-url": {
                        "operation": "export/url",
                        "input": ["task-convert"],
                        "inline": false,
                        "archive_multiple_files": false
                    }
                },
                "tag": "jobbuilder"
            })
        })
            .then(response => response.json())
            .then(result => console.log(result))

    } catch (e) {
        console.error(e);
    }
}

const API_KEY = '';