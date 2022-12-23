import re
from analyze.basic.command import Command
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpatPageJob(IamExpatPage):

    @property
    def Order(self) -> int : return 10

    reg_job_url = re.compile('/career/jobs-[^"\']+/it-technology/[^"\']+/(\d+)/?')

    def IssueCommand(self, url:str, content:str) -> list:
        if self.reg_job_url.search(url) is None: return None

        # if re.search

        return []
