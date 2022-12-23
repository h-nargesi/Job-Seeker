from django.db import connection
from analyze.agency import Agency

class Trend:

    def Create(self, state):
        pass

    def CheckTrends(self):
        context = []
        expire = Datetime.now - 5
        
        with connection.cursor() as cursor:

            cursor.execute(self.q_trend_expire, [expire])

            cursor.execute(self.q_trends)
            rows = cursor.fetchall()
            for row in rows:
                agency = {}; i = 0
                for cell in row:
                    agency[cursor.description[i][0].lower()] = cell
                    i += 1
                if agency["title"] not in agencies: continue
                context.append(agency)
        return

    q_trends = """SELECT AgencyID, State FROM Trend WHERE LastActivity > %"""
    q_trend_expire = """UPDATE Trend SET State WHERE LastActivity > %"""
    q_trend_insert = "INSERT INTO Trend (AgencyID, State) VALUES (%, %)"