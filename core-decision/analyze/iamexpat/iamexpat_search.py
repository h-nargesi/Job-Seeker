import re
from django.db import connection
from analyze.basic.command import Command
from analyze.basic.job import JobState
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpatPageSearch(IamExpatPage):

    @property
    def Order(self) -> int : return 11

    reg_search_url = re.compile('://[^/]*iamexpat\.nl/career/jobs-netherlands')
    reg_search_title = re.compile('<h1>[^<]*IT[^<]*Technology[^<]*</h1>')
    reg_job_link = re.compile('href=["\'](/career/jobs-[^"\']+/it-technology/[^"\']+/(\d+)/?)["\']')

    def IssueCommand(self, url:str, content:str) -> list:
        if self.reg_search_url.search(url) is None: return None

        if self.reg_search_title.search(content) is None:

            return [
                Command.Click('label[for="industry-260"]'), # it-technology
                Command.Click('label[for="ccareer-level-19926"]'), # entry-level
                Command.Click('label[for="career-level-19928"]'), # experienced
                Command.Click('label[for="contract-19934"]'),
                Command.Wait(3000),
                Command.Click('input[type="submit"][value="Search"]'),
            ]

        codes = set()
        with connection.cursor() as cursor:    
            for url in self.reg_job_link.findall(content):
                print("url: {}".format(url))
                code = url[1]
                print("code: {}".format(code))
                if code not in codes:
                    codes.add(code)
                    cursor.execute(IamExpatPageSearch.q_ins_job, [1, url, code, str(JobState.saved)])

        return [Command.Click('a[title="Go to next page"]')]

    q_ins_job = """INSERT INTO Job (AgencyID, Url, Code, State) VALUES (%, %, %, %)
ON CONFLICT(AgencyID, Code) DO NOTHING;
"""

