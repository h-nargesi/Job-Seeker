from analyze.basic.command import Command
from analyze.iamexpat.iamexpat_page import IamExpatPage

class IamExpatPageOther(IamExpatPage):

    @property
    def Order(self) -> int : return 100

    def IssueCommand(self, url : str, content: str) -> list:
        return [Command.Go('https://iamexpat.nl/career/jobs-netherlands')]
