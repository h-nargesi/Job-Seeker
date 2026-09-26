update Job
set Url = replace(Url, 'https://linkedin.com/', 'https://www.linkedin.com/')
where AgencyID in (select AgencyID from Agency where Title = 'LinkedIn')
  and Url like 'https://linkedin.com/%';

update Job
set Url = replace(Url, 'http://linkedin.com/', 'https://www.linkedin.com/')
where AgencyID in (select AgencyID from Agency where Title = 'LinkedIn')
  and Url like 'http://linkedin.com/%';
