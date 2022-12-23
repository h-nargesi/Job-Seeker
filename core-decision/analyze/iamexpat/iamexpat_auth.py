import re
from analyze.basic.command import Command
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpatPageAuth(IamExpatPage):

    @property
    def Order(self) -> int : return 2

    reg_login_but = re.compile('<a[^>]+href=["\']/login["\']')

    def IssueCommand(self, url : str, content: str) -> list:
        if self.reg_login_but.search(content) is None:
            return None
        
        return [Command.Click('a[href="/login"]')]