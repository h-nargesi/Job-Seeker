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
	 	(90,	'field',	'C#.NET',			'\basp\.net\b|\bc# ?\.net\b|\bc#|(\bdot ?|\.)net\b',
				'{ "resume": { "key": "DOTNET", "sub": false } }')
	,	(80,	'field',	'Java',				'\b(java|spring boot|kotlin)\b', null)
	,	(30,	'field',	'GO-Lang',			'\b(go[- ]?lang|(?-i)G[Oo])\b', 
				'{ "resume": { "key": "GOLANG" } }')
	,	(60,	'field',	'Front-End',		'\b(angular|javascript|typescript|jquery|client[- ]side script(ing)?|)\b', null)
	,	(90,	'field',	'Expert-SQL',		'\b(oracle|sql server|pl[- /]?sql|t[/-]?sql|ms[- ]?sql|sql|database|postgreSQL)\b',
				'{ "resume": { "key": "SQL" } }')
	,	(10,	'field',	'Low-Level',		'\b(html|css|json)\b',
				'{ "resume": { "key": "Web", "parent": "Front-End" } }')
	,	(30,	'field',	'Python',			'\bpython\b', null)

	--	Technologies
	,	(05,	'tech',		'Machine-Learning',	'\b((machine|deep)[- ]learning|natural[- ]network|(?-i)ML|(?-i)AI)\b', null)
	,	(10,	'tech',		'Rest-API',			'\b((web|rest)[- ]?api|web services)\b',
				'{ "resume": { "parent": "Front-End" } }')
	,	(05,	'tech',		'TensorFlow',		'\btensor[- ]?flow\b',
				'{ "resume": { "parent": "Machine-Learning" } }')
	,	(05,	'tech',		'Git',				'\bgit\b', null)

	--	Company Production
	,	(30,	'production',	'ERP',			'\berp\b', null)

	--	Company Benefits
	,	(170,	'benefit',	'Relocation',		'\brelocation(\s+(support|package))?\b|\bvisa(\s+(support|sponsorship))\b', 
				'{ "resume": null }')
	,	(02,	'salary',	'Salary',			'\bsalary\b.*?(\d[\d,]*(000|k))(.+?\b(month|year)\b)?',
				'{ "resume": null , "money": 1, "period": 4}')

	--	Keywords
	,	(02,	'keywords',	'Full-stack',		'\bfull[- ]?stack\b',
				'{ "resume": null }')
	,	(01,	'keywords',	'Developer',		'\bdeveloper\b',
				'{ "resume": null }')
	,	(02,	'keywords',	'Mid-Level',		'\b(mid|medium)[- ]level\b',
				'{ "resume": null }')
	,	(02,	'keywords',	'Frontend',			'\bfront[- ]?end\b',
				'{ "resume": null }')
	,	(02,	'keywords',	'Backend',			'\bback[- ]?end\b',
				'{ "resume": null }')

	--	Rejection
	--,	(01,	'reject',	'react',			'\b(react)\b', null)
	;

/*
	return if re.{\b(c#|(dot ?|\.)net)\b'} ok else not ok

	var @m = re.{\b(c#|(dot ?|\.)net)\b'}
	# mul, sum, div, sub
	var @s = mul @m.1 ok

	return @s
*/