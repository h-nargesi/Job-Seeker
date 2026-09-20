drop table if exists JobOption;

create table JobOption (
	JobOptionID		integer	not null	primary key,
	Efective		bit		not null	default 1,
	Category		text	not null,
	Score			integer	not null,
	Title			text	not null,
	Pattern			text	not null,
	Settings		text		null,

	unique			(Title)
);

create unique index UQ_JobOption_Name on JobOption (Title);

insert into JobOption (Score, Category, Title, Pattern, Settings)
values
	--	Programming languages

	 	(90,	'field',	'C#.NET',			'\basp\.net\b|\bc# ?\.net\b|\bc#|(\bdot ?|(?<![\w.])\.)net\b',
												'{ "resume": { "key": "DOTNET", "include_matched": false } }')
	,	(80,	'field',	'Java',				'\b(java|spring boot|kotlin)\b',
												null)
	,	(80,	'field',	'GO-Lang',			'\b(go[- ]?lang|(?-i)G[Oo](?![- ]?to\b))\b',
												'{ "resume": { "key": "GOLANG", "include_matched": false } }')
	,	(60,	'field',	'Front-End',		'\bangular(js)?\b',
												'{ "resume": { "include_matched": false } }')
	,	(60,	'field',	'Front-End-Key',	'\b(javascript|typescript|jquery|client[- ]side script(ing)?|front[- ]?end)\b',
												'{ "resume": { "key": "Front-End" } }')
	,	(90,	'field',	'Expert-SQL',		'\b(oracle|pl[- /]?sql|t[/-]?sql)\b',
												'{ "resume": { "key": "SQL", "include_matched": false } }')
	,	(90,	'field',	'Expert-SQL-Key',	'\b(sql server|ms[- ]?sql)\b',
												'{ "resume": { "key": "SQL" } }')
	,	(40,	'field',	'Simple-SQL-Key',	'\b(sql|databases?|postgre(s|sql)?|my[- ]?sql)\b',
												'{ "resume": { "key": "SQL" } }')
	,	(10,	'field',	'Low-Level',		'\b(html(\s?5)?|css(\s?3)?|json)\b',
												'{ "resume": { "key": "Web", "parent": "Front-End" } }')
	,	(30,	'field',	'Python',			'\bpython\b',
												null)
	--,	(10,	'field',	'C++',				'\bc\+\+', null)

	--	Technologies

	,	(10,	'tech',		'Machine-Learning',	'\b((machine|deep)[- ]learning|neural[- ]networks?|(?-i)ML|(?-i)AI|tensor[- ]?flow)\b',
												null)
	,	(20,	'tech',		'Entity-Framework',	'\b(entity[- ]framework( core)?|ef core)\b',
												null)
	,	(10,	'tech',		'Rest-API',			'\b(web[- ]?services|saas)\b',
												'{ "resume": { "parent": "Front-End" } }')
	,	(10,	'tech',		'Rest-API-Key',		'\b(rest[- ]?(api|ful)|web[- ]api)\b',
												'{ "resume": { "key": "Rest-API", "parent": "Front-End", "include_matched": false } }')
	,	(05,	'tech',		'Git',				'\bgit(hub|lab)?\b',
												null)
	,	(05,	'tech',		'Microservices',	'\bmicro[- ]?services?\b',
												'{ "resume": { "parent": "GOLANG", "include_matched": false } }')

	--	Company Production

	,	(30,	'production',	'ERP',			'\berp\b',
												null)

	--	Company Benefits

	,	(170,	'benefit',	'Relocation',		'(?<!no |without )\brelocation(\s+(support|package|assistance))?\b|(?<!no |without )\bvisa\s+(support|sponsorship)\b',
												'{ "resume": null }')
	,	(150,	'benefit',	'Remote',			'\b(remote(ly)?|work(ing)? from home|home[- ]office|(?-i)WFH)\b',
												'{ "resume": null }')
	,	(02,	'salary',	'Salary',			'\bsalary\b[\s\S]*?(\d[\d.,]*(000|k))([\s\S]+?\b(month|year)\b)?',
												'{ "resume": null , "money": 1, "period": 4}')

	--	Keywords

	,	(02,	'keywords',	'Full-stack',		'\bfull[- ]?stack\b',
												'{ "resume": null }')
	,	(01,	'keywords',	'Developer',		'\b(web[- ])?developer\b',
												'{ "resume": null }')
	,	(02,	'keywords',	'Mid-Level',		'\b(mid|medium)[- ]?levels?\b',
												'{ "resume": null }')
	,	(02,	'keywords',	'Backend',			'\bback[- ]?end\b',
												'{ "resume": null }')

	--	Rejection

	--,	(0,		'reject',	'react',			'\b(react)\b', null)

	--	Resume

	,	(00,	'resume',	'AWS',				'\b(aws)\b',
												null)
	,	(00,	'resume',	'CI/CD',			'\b(?-i)(CI[/-]?CD)\b',
												null)

	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/
