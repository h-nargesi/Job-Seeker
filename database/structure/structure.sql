create table Servers (
	ServerID		integer not null	primary key,
	Title			text	not null	unique,
--	Access Method
	LastDomain		text		null,
	GamePage		text		null,
	RecoveryUrl		text		null,
	RecoveryFinder	text		null
);

create unique index UQ_SRV_Title on Servers (Title);

create table Processes (
	PrcID			integer	not null	primary key,
	ServerID		integer not null,
	Title			text	not null,
--	Login Method
	Login			text		null,
	Password		text		null,
--	Setting
	HasStatistic	integer	not null	default 0,
	IsPilot			integer not null	default 1,
--	Status
	Capital			integer not null	default -1,
	CurrentPlan		integer		null,

	unique			(ServerID, Title),
	foreign key		(ServerID) references Servers (ServerID) on delete no action
);

create unique index UQ_PRC_Title on Processes (ServerID, Title);

create table Plans (
	PlanID				integer	not null	primary key,
	PrcID				integer	not null,
--	Info
	LastChanges			integer 	null,
	Taken_Rate			integer not null,
	Win_Rate			integer not null,
--	Plan Details
	IncreasingFactor	integer	not null	default 2.0,
	RecoveryFactor		integer	not null	default 5,
	MutationSize		integer	not null	default 10,
	PEX					integer not null	default 5,
	P9					integer not null	default 5,
	P26					integer not null	default 50,
	P52					integer not null	default 100,
	Method				integer not null	default 2,
	Ich_Cloud			integer not null	default 5,
	Ich_CL_SP			integer not null	default 5,
	PS_Rate				integer not null	default 5,
	CL_Rate				integer not null	default 5,
	LSA_Rate			integer not null	default 7,
	BL_Rate				integer not null	default 5,
	LSB_Rate			integer not null	default 7,
	Algorithm			text	not null,
--	Status
	Phase				integer not null	default -1,
	ConLosses			integer not null	default 0,

	foreign key			(PrcID) references Processes (PrcID) on delete no action
);

create index IX_PLANS_PrcID on Plans (PrcID);

create table Balances (
	BalanceID		integer not null	primary key,
	PrcID			integer	not null,

	LinkedTo		integer not null,
	EventTime		integer not null,
	ExpPoint		real	not null,
	Capital			integer	not null,
	ServerBenefit	integer not null,

	PlanID			integer	not null,
	Phase			integer	not null,
	ConLosses		integer not null,

	NextInvestment	integer	not null,
	NextFactor		real	not null,

	foreign key		(PrcID) references Processes (PrcID) on delete no action,
	foreign key		(PlanID) references Plans (PlanID) on delete no action
);

create index IX_BLC_View on Balances (PrcID, LinkedTo, EventTime);
create index IX_BLC_Con_Losses on Balances (PrcID, EventTime, NextFactor);

create table Messages (
	Content				text	not null,
	Type				integer	not null	default 0,
	Frecuency			integer	not null	default 1
);

create table Logs (
	AlertID			integer not null	primary key,
	PrcID			integer	not null,

	EventTime		integer not null,
	Type			integer not null,
	Content			text 	not null,

	foreign key		(PrcID) references Processes (PrcID) on delete no action
);

create index IX_LOGS_PrcID on Logs (PrcID);
