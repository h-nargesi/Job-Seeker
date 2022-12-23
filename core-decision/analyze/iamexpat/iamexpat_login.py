import re
from analyze.basic.command import Command
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpatPageLogin(IamExpatPage):

    @property
    def Order(self) -> int : return 1

    reg_login_url = re.compile('iamexpat\.com/login')

    def IssueCommand(self, url : str, content: str) -> list:
        if self.reg_login_url.search(url) is None: return None

        userpass = self.GetUserPass()
        
        return [
            Command.Fill('input[id="edit-name"]', userpass[0]),
            Command.Fill('input[id="edit-pass"]', userpass[1]),
            Command.Click('input[id="edit-submit"]')
        ]