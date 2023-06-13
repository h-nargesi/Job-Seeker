async function download_resume(jobid) {
    try {
        var response = await fetch("/job/resume?jobid=" + jobid, { method: 'POST' });
        var result = JSON.parse(response.body);
        console.log('resume-body', response.body);
        call_api(result.name, result.content);
        
    } catch (e) {
        console.error(e);
    }
}

async function call_api(name, content) {
    try {
        console.log(name, content);
        var response = await fetch("https://api.cloudconvert.com/v2/jobs", {
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
                        "input": [ "import-html" ],
                        "zoom": 1,
                        "page_width": 21,
                        "page_height": 29.7,
                        "print_background": true,
                        "display_header_footer": false,
                        "wait_until": "load",
                        "wait_time": 1000,
                        "filename": name
                    },
                    "export-1": {
                        "operation": "export/url",
                        "input": [ "task-convert" ],
                        "inline": false,
                        "archive_multiple_files": false
                    }
                },
                "tag": "jobbuilder"
            })
        });
        console.log(response);
        
    } catch (e) {
        console.error(e);
    }
}

const API_KEY = 'eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiJ9.eyJhdWQiOiIxIiwianRpIjoiZTkyMWM0MjMxZGE0NzhlNWYwZWFhYzZkMmE5ZDM3MWFjMjRhMDE0MTI5YWY4OGM4NzhlY2U2NjE0NzU0ZGQ4MGU1N2QyZWEyMmMyZjMzYjAiLCJpYXQiOjE2ODY1ODkzNjAuMzk4MTI4LCJuYmYiOjE2ODY1ODkzNjAuMzk4MTI5LCJleHAiOjQ4NDIyNjI5NjAuMzkyNTI1LCJzdWIiOiI2MTU5NDYwNSIsInNjb3BlcyI6WyJ1c2VyLnJlYWQiLCJ1c2VyLndyaXRlIiwidGFzay5yZWFkIiwidGFzay53cml0ZSIsIndlYmhvb2sucmVhZCIsIndlYmhvb2sud3JpdGUiLCJwcmVzZXQucmVhZCIsInByZXNldC53cml0ZSJdfQ.h1l_4DAkaqOdrJfXnVDFRAoUnay5zAnEKUPpL-z6lqQARVt61r4n9OfhkVqeF4Q16PagYHAU3jChTJF7ZNLL5aqqDjcDoQT4E7jwY_XJ-1QV308TnlShT9po5c7oOUf2mIubR5z6zIGh7n9guwxzyziTUHl9nafv80vI_JdS7_gOlGMk10fFYMbRLBu2TCW_WzzvGA-A4DErdrhXLmCRZqc7moI3gR6yPkE18Xd3Jbs2vDat-1JJFWU-BaHp-DaFxgFlYOp5cglEBrUp75X3WYASChD2SBFxznzniFbzFgwJja2hOspEmVX3iFFYQkluTtDMOoO_WtQTg6-3v9f8bUTa_5ptRp4R98CdXw-bMk3v_gicziDuEvKIKJAl-0wQHzrbZ3UUBxD_N_QueT07pIP_MSX0Vvx0eEFQXSjZyigGrPBxJdbsYarBk28R3DvfDkwiRzdyBKAyDrPXmJkd6wNdSuIreeh8Hc1h5NpJGdLRfFUZIVPbksnR9MyYwvsFCF9gLiA4bHnOAyCdbhPVwcllL0UQ4HvgiOW-BAF5UUueNUN2YDv5BRHOgAfdZyO-OtAN2dkN9tb-mNspL8njbnWAzIe-J6lmUBM4AL9Fcif9_PhvhtF2EUbAtegrbp1SYaiXaAUCwnABB-GfxIe_eqKba7WNWjeY1iWHi2kxFN8';