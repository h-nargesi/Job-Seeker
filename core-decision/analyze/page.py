from django.db import connection
from analyze.agency import Agency

class Page:

    __agency : Agency = None

    @property
    def Order(self) -> int : pass

    @property
    def Parent(self) -> Agency : return self.__agency

    @Parent.setter
    def Parent(self, value:Agency) : self.__agency = value

    def IssueCommand(self, url:str, content:str) -> list : return None

    def GetUserPass(self):
        result = ()
        with connection.cursor() as cursor:
            cursor.execute(self.q_user_pass, [self.Parent.Name])
            result = cursor.fetchone()
        return result

    q_user_pass = "SELECT UserName, Password FROM Agency WHERE Title = %s"